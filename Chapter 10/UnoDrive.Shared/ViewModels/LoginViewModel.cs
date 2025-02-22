﻿using System.Windows.Input;
using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using UnoDrive.Services;

namespace UnoDrive.ViewModels
{
	public class LoginViewModel
    {
		INavigationService navigation;
		ILogger logger;

		public LoginViewModel(INavigationService navigation, ILogger<LoginViewModel> logger)
		{
			this.navigation = navigation;
			this.logger = logger;
			this.logger.LogInformation("Hello logging");

			Login = new RelayCommand(OnLogin);
		}

		public string Title => "Welcome to UnoDrive!";
		public string Header => "Uno Platform ♥ OneDrive = UnoDrive";
		public string ButtonText => "Login to UnoDrive";

		public ICommand Login { get; }

		void OnLogin() =>
			navigation.NavigateToDashboard();
	}
}
