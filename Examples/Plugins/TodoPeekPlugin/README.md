# Todo Peek

`Todo Peek` adds a compact task panel to WinNotch's expanded view without adding anything to the reduced notch.

## What it does

- Shows up to `maxTasks` tasks in the expanded notch
- Lets you mark tasks complete with one click
- Adapts to light and dark themes
- Provides a vertical-friendly layout for side docking
- Supports:
  - `Todoist` with a personal API token
  - `Microsoft To Do` with a manually supplied Microsoft Graph access token

## Setup

When the plugin starts for the first time, it creates:

`%AppData%\WinNotch\Plugins\com.winnotch.todopeek\settings.json`

Default file:

```json
{
  "provider": "Todoist",
  "maxTasks": 5,
  "refreshMinutes": 5,
  "todoist": {
    "apiToken": "",
    "filter": "(today | overdue | no date) & !subtask"
  },
  "microsoftTodo": {
    "accessToken": "",
    "listName": "Tasks"
  }
}
```

## Provider notes

### Todoist

Add your personal API token to `todoist.apiToken`.

### Microsoft To Do

Add a Microsoft Graph access token with `Tasks.Read` or `Tasks.ReadWrite` to `microsoftTodo.accessToken`.

This first release expects you to provide that token manually in `settings.json`.
