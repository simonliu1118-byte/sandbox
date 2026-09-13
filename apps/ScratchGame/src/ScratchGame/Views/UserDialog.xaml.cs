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
            MessageBox.Show(this, ex.Message, "新增使用者", MessageBoxButton.OK, MessageBoxImage.Warning);
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
            row.DisplayName = renamed.DisplayName;
            row.EditName = renamed.DisplayName;
            row.IsEditing = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "修改使用者名稱", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        if (UserListBox.SelectedItem is not UserRow row)
        {
            MessageBox.Show(this, "請先選擇使用者。", "使用者", MessageBoxButton.OK, MessageBoxImage.Information);
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

        public string Id { get; init; } = string.Empty;
        public long TotalSpent { get; init; }
        public long TotalRedeemed { get; init; }
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
            => new()
            {
                Id = profile.Id,
                DisplayName = profile.DisplayName,
                EditName = profile.DisplayName,
                TotalSpent = profile.TotalSpent,
                TotalRedeemed = profile.TotalRedeemed
            };

        public UserProfile ToProfile() => new(Id, DisplayName, TotalSpent, TotalRedeemed);

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
