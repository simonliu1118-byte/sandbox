using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using ScratchGame.Data;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class UserDialog : Window
{
    private readonly CatalogService _catalog;
    private readonly UserProfileService _profiles;
    private readonly ObservableCollection<UserRow> _users;

    public UserProfile? SelectedUser { get; private set; }
    private readonly string? _currentUserId;

    public UserDialog(
        IReadOnlyList<UserProfile> users,
        UserProfile? currentUser,
        CatalogService catalog,
        AppDatabase database)
    {
        InitializeComponent();
        UiAssetLoader.TrySetImage(DialogBackgroundImage, UiAssetLoader.UiPath("dialog_bg.png"));
        _catalog = catalog;
        _profiles = new UserProfileService(database);
        _users = new ObservableCollection<UserRow>(users.Select(UserRow.FromProfile));
        _currentUserId = currentUser?.Id;
        UserListBox.ItemsSource = _users;

        var current = currentUser is null
            ? null
            : _users.FirstOrDefault(user => user.Id == currentUser.Id);
        UserListBox.SelectedItem = current ?? _users.FirstOrDefault();
    }

    private async void AddUser_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var user = await _catalog.CreateUserAsync(NewUserNameTextBox.Text);
            var row = UserRow.FromProfile(user);
            _users.Add(row);
            UserListBox.SelectedItem = row;
            UserListBox.ScrollIntoView(row);
            NewUserNameTextBox.Clear();
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "新增使用者", ex.Message);
        }
    }

    private void EditName_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not UserRow row)
            return;

        row.EditName = row.DisplayName;
        row.IsEditing = true;
    }

    private async void SaveName_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not UserRow row)
            return;

        await SaveNameAsync(row);
    }

    private async void EditNameTextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && (sender as FrameworkElement)?.DataContext is UserRow cancelRow)
        {
            cancelRow.IsEditing = false;
            cancelRow.EditName = cancelRow.DisplayName;
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || (sender as FrameworkElement)?.DataContext is not UserRow row)
            return;

        await SaveNameAsync(row);
        e.Handled = true;
    }

    private async Task SaveNameAsync(UserRow row)
    {
        try
        {
            var renamed = await _profiles.RenameAsync(row.Id, row.EditName);
            row.ApplyProfile(renamed);
            row.IsEditing = false;
            UpdateOwnerSummaryIfCurrent(renamed);
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "修改使用者名稱", ex.Message);
        }
    }

    private async void ResetStats_OnClick(object sender, RoutedEventArgs e)
    {
        if (UserListBox.SelectedItem is not UserRow row)
        {
            GameModal.Info(this, "重置損益", "請先選擇要重置的使用者。");
            return;
        }

        if (!GameModal.Confirm(
                this,
                "重置損益",
                $"要將「{row.DisplayName}」目前的投入、兌獎與損益歸零嗎？\n\n這不會刪除彩券歷史，也不會改變票池。",
                "確定重置",
                "取消"))
            return;

        try
        {
            var reset = await _profiles.ResetStatsAsync(row.Id);
            row.ApplyProfile(reset);
            UpdateOwnerSummaryIfCurrent(reset);
        }
        catch (Exception ex)
        {
            GameModal.Warning(this, "重置損益", ex.Message);
        }
    }

    private void UpdateOwnerSummaryIfCurrent(UserProfile profile)
    {
        if (profile.Id != _currentUserId || Owner is not MainWindow owner)
            return;

        if (owner.FindName("UserSummaryText") is not System.Windows.Controls.TextBlock summary)
            return;

        var netText = profile.Net >= 0
            ? $"+${profile.Net:N0}"
            : $"-${Math.Abs(profile.Net):N0}";
        summary.Text = $"{profile.DisplayName}　損益 {netText}";
    }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        if (UserListBox.SelectedItem is not UserRow row)
        {
            GameModal.Info(this, "使用者", "請先選擇使用者。");
            return;
        }

        SelectedUser = row.ToProfile();
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private sealed class UserRow : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private string _editName = string.Empty;
        private bool _isEditing;
        private long _totalSpent;
        private long _totalRedeemed;

        public string Id { get; init; } = string.Empty;

        public long TotalSpent
        {
            get => _totalSpent;
            private set
            {
                _totalSpent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Net));
            }
        }

        public long TotalRedeemed
        {
            get => _totalRedeemed;
            private set
            {
                _totalRedeemed = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Net));
            }
        }

        public long Net => TotalRedeemed - TotalSpent;

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        public string EditName
        {
            get => _editName;
            set { _editName = value; OnPropertyChanged(); }
        }

        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static UserRow FromProfile(UserProfile profile)
        {
            var row = new UserRow { Id = profile.Id };
            row.ApplyProfile(profile);
            return row;
        }

        public void ApplyProfile(UserProfile profile)
        {
            DisplayName = profile.DisplayName;
            EditName = profile.DisplayName;
            TotalSpent = profile.TotalSpent;
            TotalRedeemed = profile.TotalRedeemed;
        }

        public UserProfile ToProfile() => new(Id, DisplayName, TotalSpent, TotalRedeemed);

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
