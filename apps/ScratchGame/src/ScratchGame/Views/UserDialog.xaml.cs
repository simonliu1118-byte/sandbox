using System.Collections.ObjectModel;
using System.Windows;
using ScratchGame.Models;
using ScratchGame.Services;

namespace ScratchGame.Views;

public partial class UserDialog : Window
{
    private readonly CatalogService _catalog;
    private readonly ObservableCollection<UserProfile> _users;

    public UserProfile? SelectedUser { get; private set; }

    public UserDialog(
        IReadOnlyList<UserProfile> users,
        UserProfile? currentUser,
        CatalogService catalog)
    {
        InitializeComponent();
        _catalog = catalog;
        _users = new ObservableCollection<UserProfile>(users);
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
            _users.Add(user);
            UserListBox.SelectedItem = user;
            UserListBox.ScrollIntoView(user);
            NewUserNameTextBox.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "新增使用者", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Select_OnClick(object sender, RoutedEventArgs e)
    {
        if (UserListBox.SelectedItem is not UserProfile user)
        {
            MessageBox.Show(this, "請先選擇使用者。", "使用者", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedUser = user;
        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
