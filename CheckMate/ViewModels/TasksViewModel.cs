﻿using CheckMate.Data;
using CheckMate.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
//using ReplayKit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace CheckMate.ViewModels
{
    public partial class TasksViewModel : ObservableObject
    {

        // Database context for interacting with tasks in the SQLite database
        private readonly DatabaseContext _context;
        public IRelayCommand SaveTaskAsyncCommand { get; }
        public IRelayCommand GoToCreateTaskCommand { get; }
        public IRelayCommand GoToHomeCommand { get; }

        // Constructor that initializes the ViewModel with a DatabaseContext
        public TasksViewModel(DatabaseContext context)
        {
            // Assign the provided DatabaseContext to the private field _context
            _context = context;

            SaveTaskAsyncCommand = new RelayCommand(async () => await SaveTaskASync());
            GoToCreateTaskCommand = new RelayCommand(async () => await NavigateToCreateTask());
            GoToHomeCommand = new RelayCommand(async () => await NavigateToHome());
        }

        [RelayCommand]
        private async Task NavigateToCreateTask()
        {
            await Shell.Current.GoToAsync("CreateTaskPage");
        }

        [RelayCommand]
        private async Task NavigateToHome()
        {
            await Shell.Current.GoToAsync("MainPage");
        }

        [RelayCommand]
        private async Task SaveAndNavigate()
        {
            await SaveTaskASync();
            await NavigateToHome();
        }

        // Collection of UserTasks to be displayed in the UI
        [ObservableProperty]
        private ObservableCollection<UserTask> _tasks = new();

        // UserTask currently being operated on (either created or updated)
        [ObservableProperty]
        private UserTask _operatingTask = new();

        // Indicates whether an asynchronous operation is in progress
        [ObservableProperty]
        private bool _isBusy;

        // Text indicating the current status of an asynchronous operation
        [ObservableProperty]
        private string _busyText;

        // Come here when adding subtasks to database

        // Asynchronously loads tasks from the database
        public async Task LoadTaskAsync()
        {
            // ExecuteAsync is a utility method to handle UI state during an asynchronous operation
            await ExecuteAsync(async () =>
            {
                // Retrieve tasks from the database using the DatabaseContext
                var tasks = await _context.GetAllAsync<UserTask>();

                // Check if tasks were retrieved and if there are any existing tasks
                if (tasks is not null && tasks.Any())
                {
                    // Ensure the tasks collection is initialized, or create a new one
                    tasks ??= new ObservableCollection<UserTask>();

                    // Clear existing tasks and add new ones
                    foreach (var task in Tasks)
                    {
                        Tasks.Add(task);
                    }
                }
            }, "Fetching Tasks..."); // Display "Fetching Tasks..." as the status during the operation

        }

        // Command method to set the operating task
        [RelayCommand]
        private void SetOperatingTask(UserTask? task)
        {
            OperatingTask = task ?? new();
        }

        // Command method to asynchronously save the operating task to the database
        [RelayCommand]
        private async Task SaveTaskASync()
        {
            // Check if the operating task is null; return if true
            if (OperatingTask is null)
            {
                return;
            }

            var (isValid, errorMessage) = OperatingTask.Validate();

            if (!isValid)
            {
                await Shell.Current.DisplayAlert("Validation Error", errorMessage, "Confirm");
                return;
            }

            // Determine the busy text based on whether the task is being created or updated
            var busyText = OperatingTask.Id == 0 ? "Creating Task..." : "Updating Task...";

            // ExecuteAsync is a utility method to handle UI state during an asynchronous operation
            await ExecuteAsync(async () =>
            {
                if (OperatingTask.Id == 0)
                {
                    // Creating the task
                    await _context.AddTaskAsync<UserTask>(OperatingTask);
                    Tasks.Add(OperatingTask);
                }
                else
                {
                    // Updating the task
                    await _context.UpdateTaskAsync<UserTask>(OperatingTask);

                    // Create a copy of the updated task for proper UI update
                    var taskCopy = OperatingTask.Clone();

                    // Replace the existing task in the collection with the updated copy
                    var index = Tasks.IndexOf(OperatingTask);
                    Tasks.RemoveAt(index);
                    Tasks.Insert(index, taskCopy);
                }

                // Reset the operating task and update the UI
                SetOperatingTaskCommand.Execute(new());
            }, busyText);

        }

        // Command method to asynchronously delete a task by its ID
        [RelayCommand]
        private async Task DeleteTaskAsync(int id)
        {
            // ExecuteAsync is a utility method to handle UI state during an asynchronous operation
            await ExecuteAsync(async () =>
            {
                // Attempt to delete the task from the database
                if (await _context.DeleteTaskByKeyAsync<UserTask>(id))
                {
                    // If deletion is successful, remove the task from the collection
                    var task = Tasks.FirstOrDefault(p => p.Id == id);
                    Tasks.Remove(task);
                }
                else
                {
                    // Display an alert if deletion fails
                    await Shell.Current.DisplayAlert("Delete Error", "Task was not deleted", "OK");
                }
            }, "Deleting task"); // Display "Deleting task..." as the status during the operation

            // Update UI state to indicate that a deletion operation is in progress
            IsBusy = true;
            BusyText = "Deleting task...";
        }

        // Asynchronously executes an operation while handling UI state
        private async Task ExecuteAsync(Func<Task> operation, string? busyText = null)
        {
            // Set UI state to indicate that an asynchronous operation is in progress
            IsBusy = true;

            // Set the busy text to the provided value or a default value ("Processing...")
            BusyText = busyText ?? "Processing...";

            try
            {
                // Invoke the provided operation asynchronously
                await operation?.Invoke();
            }
            finally
            {
                // Reset UI state after the operation completes, indicating that processing is done
                IsBusy = false;
                BusyText = "Processing...";
            }
        }
    }
}