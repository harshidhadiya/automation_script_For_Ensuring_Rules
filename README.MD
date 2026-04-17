# Bug Audit Automation Tool – Usage Guide

This document explains how to use the automation tool via command-line arguments and interactive mode.

---

# 1. Configuration Setup

Before running, ensure you have a valid `config.json`.

## Example

```json
{
  "AppSettings": {
    "issues_url": "https://api.atlassian.com/ex/jira/{CLOUD_KEY}/rest/api/3/search/jql",
    "comment_url": "https://api.atlassian.com/ex/jira/{CLOUD_KEY}/rest/api/3/issue/{issueKey}/comment",
    "api_email": "YOUR_JIRA_ACCOUNT_GMAIL.com",
    "api_key": "REQUIRED: Jira scoped API token",
    "log_file_path": "D:/work2/BugAuditScript/logs/app.log",
    "credential_file_path": "D:/work2/BugAuditScript/credentials.json",
    "output": "D:/work2/BugAuditScript/output",
    "spread_sheet_id": "TARGET_GOOGLE_SHEET_ID"
  }
}
```

### Notes

* `log_file_path`:
  If empty → log file will be created in the same folder as `.exe`

* `credential_file_path`:
  Required only if using `-g` flag (Google Sheets)

* `output`:
  If empty → output will be generated in `.exe` directory

* `spread_sheet_id`:
  Required only if pushing data to Google Sheets

---

# 2. Running the Application

## Option 1: Interactive Mode (Manual Run)

```bash
MyApp.exe --configPath="D:\MyAutomation\config"
```

### Behavior

* CLI menu appears
* User selects:

  * Number of days (e.g. `7`)
  * OR `*` for custom date range

---

## Option 2: Automated Mode (Scheduler / Background)

### Fetch by Days

```bash
MyApp.exe --configPath="D:\MyAutomation\config" -sd -day=7
```

### Behavior

* Fetch last 7 days data
* `-day=7` is optional (default = 7)
* No user input required
* Suitable for Task Scheduler

---

# 3. Optional Flags

## Delete Mode

```bash
-d
```

### Example

```bash
MyApp.exe --configPath="D:\MyAutomation\config" -sd -d
```

### Behavior

* Skips storing CSV file
* Used when only processing is needed

---

## Google Sheet Push Mode

```bash
-g
```

### Example

```bash
MyApp.exe --configPath="D:\MyAutomation\config" -sd -g
```

### Behavior

* Pushes data to Google Sheets
* Requires:

  * `credential_file_path`
  * `spread_sheet_id`

---

# 4. Argument Explanation

| Argument       | Description                                        |
| -------------- | -------------------------------------------------- |
| `--configPath` | Path to folder containing `config.json` (required) |
| `-sd`          | Enables background/scheduled mode                  |
| `-day`         | Number of days to fetch data                       |
| `-d`           | Skip CSV file creation                             |
| `-g`           | Push data to Google Sheets                         |

---

# 5. Execution Modes

## With `-sd`

* No interactive prompts
* Runs silently
* Designed for automation and scheduling

## Without `-sd`

* Runs in interactive mode
* Prompts user for input

---

# 6. Example Use Cases

## Manual Run

```bash
MyApp.exe --configPath="D:\MyAutomation\config"
```

## Daily Automation

```bash
MyApp.exe --configPath="D:\MyAutomation\config" -sd
```

## Weekly Data Fetch

```bash
MyApp.exe --configPath="D:\MyAutomation\config" -sd -day=7
```

## Push to Google Sheets

```bash
MyApp.exe --configPath="D:\MyAutomation\config" -sd -g
```

## Run Without Saving CSV

```bash
MyApp.exe --configPath="D:\MyAutomation\config" -sd -d
```

---

# 7. Important Notes

* Always provide correct `configPath`
* Use quotes if path contains spaces
* Use `-sd` for any automated or scheduled execution
* Ensure required fields are configured when using `-g`
* Logs are written to the configured log file path

---

# 8. Summary

| Mode        | Purpose                   |
| ----------- | ------------------------- |
| Interactive | Manual execution          |
| `-sd`       | Background automation     |
| `-d`        | Skip CSV storage          |
| `-g`        | Push data to Google Sheet |

---

Key Rule:

Use `-sd` when running via scheduler or automation.
