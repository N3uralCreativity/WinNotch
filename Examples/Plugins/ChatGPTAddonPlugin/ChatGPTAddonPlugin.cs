using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinNotch.Plugins;

namespace ChatGPTAddonPlugin;

/// <summary>
/// Example plugin that adds ChatGPT voice and text integration to WinNotch.
/// This is a simplified demo - a real implementation would use the OpenAI API.
/// </summary>
[WinNotchPlugin("com.winnotch.chatgpt", "ChatGPT Add-on", "1.0.0", "WinNotch Team")]
[PluginPermission("network", "Required to communicate with OpenAI API")]
public class ChatGPTAddonPlugin : PluginBase, IUIPlugin
{
    private ChatGPTPanel? _chatPanel;
    private TextBlock? _closedIndicator;

    public override string Id => "com.winnotch.chatgpt";
    public override string Name => "ChatGPT Add-on";
    public override string Version => "1.0.0";
    public override string Author => "WinNotch Team";
    public override string Description => "Adds ChatGPT voice and text integration with easy access from the notch";

    public override async Task InitializeAsync(IPluginContext context)
    {
        await base.InitializeAsync(context);
        Context?.Log("ChatGPT Add-on initialized!");
    }

    public UIElement? GetUIElement(UIPluginLocation location)
    {
        return location switch
        {
            UIPluginLocation.ClosedContent => CreateClosedIndicator(),
            UIPluginLocation.CustomTab => CreateChatPanel(),
            UIPluginLocation.Settings => CreateSettingsPanel(),
            _ => null
        };
    }

    public void OnNotchStateChanged(Models.NotchState newState)
    {
        if (newState == Models.NotchState.Open && _chatPanel != null)
        {
            _chatPanel.Focus();
        }
    }

    private UIElement CreateClosedIndicator()
    {
        _closedIndicator = new TextBlock
        {
            Text = "💬",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
            ToolTip = "ChatGPT - Click to open"
        };

        return _closedIndicator;
    }

    private UIElement CreateChatPanel()
    {
        _chatPanel = new ChatGPTPanel(Context!);
        return _chatPanel;
    }

    private UIElement CreateSettingsPanel()
    {
        var panel = new StackPanel { Margin = new Thickness(10) };

        var titleBlock = new TextBlock
        {
            Text = "ChatGPT Settings",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 10)
        };
        panel.Children.Add(titleBlock);

        var apiKeyLabel = new TextBlock
        {
            Text = "API Key:",
            Margin = new Thickness(0, 0, 0, 5)
        };
        panel.Children.Add(apiKeyLabel);

        var apiKeyBox = new TextBox
        {
            Width = 300,
            Margin = new Thickness(0, 0, 0, 10),
            Text = "sk-..." // Placeholder
        };
        panel.Children.Add(apiKeyBox);

        var voiceCheckBox = new CheckBox
        {
            Content = "Enable Voice Input",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 5)
        };
        panel.Children.Add(voiceCheckBox);

        var saveButton = new Button
        {
            Content = "Save Settings",
            Width = 100,
            Margin = new Thickness(0, 10, 0, 0)
        };
        saveButton.Click += (s, e) =>
        {
            Context?.Log("ChatGPT settings saved");
            MessageBox.Show("Settings saved!", "ChatGPT Add-on");
        };
        panel.Children.Add(saveButton);

        return panel;
    }
}

/// <summary>
/// Custom chat panel UI component.
/// </summary>
internal class ChatGPTPanel : Grid
{
    private readonly IPluginContext _context;
    private readonly TextBox _inputBox;
    private readonly ScrollViewer _chatScroll;
    private readonly StackPanel _chatStack;
    private readonly Button _voiceButton;

    public ChatGPTPanel(IPluginContext context)
    {
        _context = context;

        // Setup grid layout
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Chat history area
        _chatStack = new StackPanel { Margin = new Thickness(10) };
        _chatScroll = new ScrollViewer
        {
            Content = _chatStack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 250
        };
        SetRow(_chatScroll, 0);
        Children.Add(_chatScroll);

        // Input area
        var inputGrid = new Grid { Margin = new Thickness(10) };
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _inputBox = new TextBox
        {
            Height = 32,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(8, 0, 8, 0)
        };
        _inputBox.KeyDown += OnInputKeyDown;
        SetColumn(_inputBox, 0);
        inputGrid.Children.Add(_inputBox);

        _voiceButton = new Button
        {
            Content = "🎤",
            Width = 40,
            Height = 32,
            Margin = new Thickness(5, 0, 0, 0)
        };
        _voiceButton.Click += OnVoiceButtonClick;
        SetColumn(_voiceButton, 1);
        inputGrid.Children.Add(_voiceButton);

        var sendButton = new Button
        {
            Content = "Send",
            Width = 60,
            Height = 32,
            Margin = new Thickness(5, 0, 0, 0)
        };
        sendButton.Click += OnSendButtonClick;
        SetColumn(sendButton, 2);
        inputGrid.Children.Add(sendButton);

        SetRow(inputGrid, 1);
        Children.Add(inputGrid);

        // Welcome message
        AddMessage("ChatGPT", "Hello! How can I help you today?", false);
    }

    private void OnInputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            SendMessage();
        }
    }

    private void OnSendButtonClick(object sender, RoutedEventArgs e)
    {
        SendMessage();
    }

    private async void OnVoiceButtonClick(object sender, RoutedEventArgs e)
    {
        _voiceButton.Content = "⏺️";
        _voiceButton.IsEnabled = false;

        // Simulate voice recording
        await Task.Delay(2000);

        _voiceButton.Content = "🎤";
        _voiceButton.IsEnabled = true;

        AddMessage("You", "This is a voice message (simulated)", true);
        await SimulateResponse();
    }

    private async void SendMessage()
    {
        var message = _inputBox.Text.Trim();
        if (string.IsNullOrEmpty(message)) return;

        AddMessage("You", message, true);
        _inputBox.Clear();

        await SimulateResponse();
    }

    private async Task SimulateResponse()
    {
        // Simulate API call delay
        await Task.Delay(1000);

        var responses = new[]
        {
            "That's an interesting question! Based on my knowledge...",
            "I'd be happy to help with that. Here's what I think...",
            "Great question! Let me explain...",
            "I understand what you're asking. The answer is..."
        };

        var random = new Random();
        var response = responses[random.Next(responses.Length)];

        AddMessage("ChatGPT", response, false);
    }

    private void AddMessage(string sender, string message, bool isUser)
    {
        var messageBlock = new Border
        {
            Background = new SolidColorBrush(isUser ? Color.FromRgb(0, 122, 255) : Color.FromRgb(50, 50, 54)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8),
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MaxWidth = 400
        };

        var textStack = new StackPanel();

        var senderBlock = new TextBlock
        {
            Text = sender,
            FontWeight = FontWeights.Bold,
            FontSize = 11,
            Foreground = Brushes.White,
            Margin = new Thickness(0, 0, 0, 4)
        };
        textStack.Children.Add(senderBlock);

        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.White,
            FontSize = 13
        };
        textStack.Children.Add(messageText);

        messageBlock.Child = textStack;
        _chatStack.Children.Add(messageBlock);

        // Auto-scroll to bottom
        _chatScroll.ScrollToBottom();
    }
}
