// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using WinRT;

namespace Files.App.Utils.Cloud
{
	public sealed partial class CloudDriveSyncStatusUI : ObservableObject
	{
		public string? Glyph { get; }

		private readonly string? _themedIconStyleKey;

		// Looked up on read: instances are built on background threads, where resource lookups throw RPC_E_WRONG_THREAD
		public Style? ThemedIconStyle
		{
			[DynamicWindowsRuntimeCast(typeof(Style))]
			get => _themedIconStyleKey is null ? null : (Style)Application.Current.Resources[_themedIconStyleKey];
		}

		public CloudDriveSyncStatus SyncStatus { get; }

		public bool LoadSyncStatus { get; }

		public string SyncStatusString { get; } = Strings.CloudDriveSyncStatus_Unknown.GetLocalizedResource();

		public CloudDriveSyncStatusUI()
		{
			SyncStatus = CloudDriveSyncStatus.Unknown;
		}

		private CloudDriveSyncStatusUI(CloudDriveSyncStatus syncStatus)
		{
			SyncStatus = syncStatus;
		}

		private CloudDriveSyncStatusUI(string glyph, string themedIconStyleKey, CloudDriveSyncStatus syncStatus, string SyncStatusStringKey)
		{
			SyncStatus = syncStatus;
			Glyph = glyph;
			_themedIconStyleKey = themedIconStyleKey;
			LoadSyncStatus = true;
			SyncStatusString = SyncStatusStringKey.GetLocalizedResource();
		}

		public static CloudDriveSyncStatusUI FromCloudDriveSyncStatus(CloudDriveSyncStatus syncStatus) => syncStatus switch
		{
			// File
			CloudDriveSyncStatus.FileOnline
				=> new CloudDriveSyncStatusUI("\uE753", "App.ThemedIcons.Status.Cloud", syncStatus, "CloudDriveSyncStatus_Online"),
			CloudDriveSyncStatus.FileOffline
				=> new CloudDriveSyncStatusUI("\uE73E", "App.ThemedIcons.Status.Available", syncStatus, "CloudDriveSyncStatus_Offline"),
			CloudDriveSyncStatus.FileOfflinePinned
				=> new CloudDriveSyncStatusUI("\uE73E", "App.ThemedIcons.Status.KeepOffline", syncStatus, "CloudDriveSyncStatus_Offline"),
			CloudDriveSyncStatus.FileSync
				=> new CloudDriveSyncStatusUI("\uE895", "App.ThemedIcons.Status.Syncing", syncStatus, "CloudDriveSyncStatus_Sync"),

			//// Folder
			CloudDriveSyncStatus.FolderOnline or CloudDriveSyncStatus.FolderOfflinePartial
				=> new CloudDriveSyncStatusUI("\uE753", "App.ThemedIcons.Status.Cloud", syncStatus, "CloudDriveSyncStatus_PartialOffline"),
			CloudDriveSyncStatus.FolderOfflineFull or CloudDriveSyncStatus.FolderEmpty
				=> new CloudDriveSyncStatusUI("\uE73E", "App.ThemedIcons.Status.Available", syncStatus, "CloudDriveSyncStatus_Offline"),
			CloudDriveSyncStatus.FolderOfflinePinned
				=> new CloudDriveSyncStatusUI("\uE73E", "App.ThemedIcons.Status.KeepOffline", syncStatus, "CloudDriveSyncStatus_Offline"),
			CloudDriveSyncStatus.FolderExcluded
				=> new CloudDriveSyncStatusUI("\uF140", "App.ThemedIcons.Status.Unavailable", syncStatus, "CloudDriveSyncStatus_Excluded"),

			// Unknown
			_ => new CloudDriveSyncStatusUI(syncStatus),
		};
	}
}
