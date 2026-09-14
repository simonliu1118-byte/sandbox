using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ScratchGame.Views;

public partial class NewTicketDialog
{
    private bool _build6CardLayoutApplied;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_build6CardLayoutApplied)
            return;

        _build6CardLayoutApplied = true;
        Width = 1000;
        Height = 760;

        TicketListBox.ItemsPanel = (ItemsPanelTemplate)XamlReader.Parse("""
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
              <WrapPanel Orientation="Horizontal" IsItemsHost="True"/>
            </ItemsPanelTemplate>
            """);

        TicketListBox.ItemContainerStyle = (Style)XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListBoxItem">
              <Setter Property="Width" Value="278"/>
              <Setter Property="Height" Value="236"/>
              <Setter Property="Margin" Value="8"/>
              <Setter Property="Padding" Value="0"/>
              <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
              <Setter Property="VerticalContentAlignment" Value="Stretch"/>
              <Setter Property="Background" Value="Transparent"/>
              <Setter Property="Cursor" Value="Hand"/>
              <Setter Property="Template">
                <Setter.Value>
                  <ControlTemplate TargetType="ListBoxItem">
                    <Border x:Name="Card" Background="#3A1A14" BorderBrush="#73503A" BorderThickness="1.5" CornerRadius="14" Padding="10">
                      <ContentPresenter/>
                    </Border>
                    <ControlTemplate.Triggers>
                      <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Card" Property="Background" Value="#47231A"/>
                        <Setter TargetName="Card" Property="BorderBrush" Value="#B17A43"/>
                      </Trigger>
                      <Trigger Property="IsSelected" Value="True">
                        <Setter TargetName="Card" Property="Background" Value="#51291C"/>
                        <Setter TargetName="Card" Property="BorderBrush" Value="#FFD05A"/>
                        <Setter TargetName="Card" Property="BorderThickness" Value="3"/>
                      </Trigger>
                    </ControlTemplate.Triggers>
                  </ControlTemplate>
                </Setter.Value>
              </Setter>
            </Style>
            """);

        TicketListBox.ItemTemplate = (DataTemplate)XamlReader.Parse("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Grid>
                <Grid.RowDefinitions>
                  <RowDefinition Height="126"/>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                  <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <Border Grid.Row="0" Background="#24110D" CornerRadius="9" ClipToBounds="True">
                  <Grid>
                    <Image Source="{Binding ThumbnailPath}" Stretch="Uniform" Margin="4"/>
                    <TextBlock Text="找不到票面美術" HorizontalAlignment="Center" VerticalAlignment="Center" Foreground="#B99376" FontSize="13">
                      <TextBlock.Style>
                        <Style TargetType="TextBlock">
                          <Setter Property="Visibility" Value="Collapsed"/>
                          <Style.Triggers>
                            <DataTrigger Binding="{Binding ThumbnailPath}" Value="{x:Null}"><Setter Property="Visibility" Value="Visible"/></DataTrigger>
                          </Style.Triggers>
                        </Style>
                      </TextBlock.Style>
                    </TextBlock>
                  </Grid>
                </Border>
                <TextBlock Grid.Row="1" Text="{Binding Ticket.DisplayName}" Margin="2,8,2,0" FontSize="17" FontWeight="Bold" Foreground="#FFF0CF" TextTrimming="CharacterEllipsis"/>
                <Grid Grid.Row="2" Margin="2,5,2,0">
                  <Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition/></Grid.ColumnDefinitions>
                  <TextBlock Foreground="#D9B999" FontSize="13"><Run Text="面額 $"/><Run Text="{Binding Ticket.Price, StringFormat={}{0:N0}}"/></TextBlock>
                  <TextBlock Grid.Column="1" Text="{Binding BatchText}" HorizontalAlignment="Right" Foreground="#C79E79" FontSize="13"/>
                </Grid>
                <Grid Grid.Row="3" Margin="2,4,2,0">
                  <TextBlock Text="發行中獎率" Foreground="#A98268" FontSize="11"/>
                  <TextBlock Text="{Binding Ticket.PublishedWinRate, StringFormat={}{0:P2}}" HorizontalAlignment="Right" Foreground="#FFD363" FontSize="17" FontWeight="Bold"/>
                </Grid>
              </Grid>
            </DataTemplate>
            """);

        ScrollViewer.SetHorizontalScrollBarVisibility(TicketListBox, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(TicketListBox, ScrollBarVisibility.Visible);
    }
}
