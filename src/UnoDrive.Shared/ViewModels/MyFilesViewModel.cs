﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using UnoDrive.Data;
using UnoDrive.Models;
using UnoDrive.Mvvm;
using UnoDrive.Services;
using Windows.Networking.Connectivity;
using Windows.UI.Xaml.Controls;

namespace UnoDrive.ViewModels
{
	public class MyFilesViewModel : ObservableObject, IInitialize
	{
		Location location = new Location();
		readonly IGraphFileService graphFileService;
		readonly ILogger logger;
		readonly INetworkConnectivityService networkConnectivity;
		public MyFilesViewModel(IGraphFileService graphFileService, ILogger<MyFilesViewModel> logger, INetworkConnectivityService networkConnectivity)
		{
			this.graphFileService = graphFileService;
			this.logger = logger;
			this.networkConnectivity = networkConnectivity;

			Forward = new AsyncRelayCommand(OnForwardAsync);
			Back = new AsyncRelayCommand(OnBackAsync);

			FilesAndFolders = new List<OneDriveItem>();
			this.networkConnectivity.NetworkStatusChanged += OnNetworkStatusChanged;
		}

		public ICommand Forward { get; }
		public ICommand Back { get; }

		// We are not using an ObservableCollection
		// by design. It can create significant performance
		// problems and it wasn't loading correctly on Android.
		List<OneDriveItem> filesAndFolders;
		public List<OneDriveItem> FilesAndFolders
		{
			get => filesAndFolders;
			set
			{
				SetProperty(ref filesAndFolders, value);
				OnPropertyChanged(nameof(CurrentFolderPath));
				OnPropertyChanged(nameof(IsPageEmpty));
			}
		}

		public bool IsPageEmpty => !FilesAndFolders.Any();

		public string CurrentFolderPath => FilesAndFolders.FirstOrDefault()?.Path;

		public bool IsNetworkConnected =>
			networkConnectivity.Connectivity == NetworkConnectivityLevel.InternetAccess;

		string noDataMessage;
		public string NoDataMessage
		{
			get => noDataMessage;
			set => SetProperty(ref noDataMessage, value);
		}

		bool isBusy;
		public bool IsBusy
		{
			get => isBusy;
			set => SetProperty(ref isBusy, value);
		}

		public async void ItemClick(object sender, ItemClickEventArgs args)
		{
			if (args.ClickedItem is OneDriveItem driveItem)
			{
				if (driveItem.Type == OneDriveItemType.Folder)
				{
					await LoadDataAsync(driveItem.Id);
					location.Forward = new Location
					{
						Id = driveItem.Id,
						Back = location
					};

					location = location.Forward;

				}
				else
				{
					// TODO - open file
				}
			}
		}

		void OnNetworkStatusChanged(object sender) =>
			OnPropertyChanged(nameof(IsNetworkConnected));

		async Task OnForwardAsync()
		{
			if (!location.CanMoveForward)
				return;

			// This doesn't appear to be working
			await LoadDataAsync(location.Forward.Id);
			location = location.Forward;
		}

		async Task OnBackAsync()
		{
			if (!location.CanMoveBack)
				return;

			await LoadDataAsync(location.Back.Id);
			location = location.Back;
		}

		async Task LoadDataAsync(string pathId = null)
		{
			// TODO - Test IsBusy logic by adding a task delay
			try
			{
				IsBusy = true;

				IEnumerable<OneDriveItem> data;
				if (string.IsNullOrEmpty(pathId))
					data = await graphFileService.GetRootFiles(UpdateFiles);
				else
					data = await graphFileService.GetFiles(pathId, UpdateFiles);

				UpdateFiles(data);
			}
			catch (Exception ex)
			{
				logger.LogError(ex, ex.Message);
			}
			finally
			{
				IsBusy = false;
			}

			void UpdateFiles(IEnumerable<OneDriveItem> files, bool isCached = false)
			{
				if (files == null)
				{
					// This doesn't appear to be getting triggered correctly
					NoDataMessage = "Unable to retrieve data from API, check network connection";
					logger.LogInformation("No data retrieved from API, ensure you have a stable internet connection");
					return;
				}
				else if (!files.Any())
					NoDataMessage = "No files or folders";

				FilesAndFolders = files.ToList();

				if (files.Any() && isCached)
					IsBusy = false;
			}
		}

		async Task IInitialize.InitializeAsync()
		{
			await LoadDataAsync();

			var firstItem = FilesAndFolders.FirstOrDefault();
			if (firstItem != null)
				location.Id = firstItem.PathId;
		}
	}
}
