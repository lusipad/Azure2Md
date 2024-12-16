# Azure2Md

[简体中文](README.md) | English

A tool to export Azure DevOps work items to Markdown reports.

## Features

- Support multiple project work item export
- Support merged or independent project reports
- Support complete work item hierarchy (Feature -> User Story -> Task)
- Generate multi-level Gantt chart views
  - Feature + User Story view
  - User Story + Task view
- Group tasks by team member
- Support custom queries or existing queries
- Display complete time information for work items
- Support task status visualization (active/done)
- Support multiple languages (Chinese/English/Auto-detect)

## Configuration

Configure in `appsettings.json`:

```json
{
    "TfsUrl": "https://dev.azure.com/your-org",
    "PersonalAccessToken": "your-pat-token",
    "ReportSettings": {
        "MergeProjects": true,
        "MergedTitle": "Multi-Project Report",
        "Language": "auto"  // Options: auto, zh-CN, en-US
    },
    "Projects": [
        {
            "ProjectName": "project-name",
            "Query": {
                "UseExistingQuery": false,
                "QueryPath": "Shared Queries/query-path",
                "CustomWiql": null
            }
        }
    ]
}
```

### Configuration Items

- `TfsUrl`: Azure DevOps organization URL
- `PersonalAccessToken`: Personal Access Token (PAT)
- `ReportSettings`: Report generation settings
  - `MergeProjects`: Whether to merge multiple project reports
  - `MergedTitle`: Title for merged report
  - `Language`: Language setting
    - `auto`: Auto-detect system language
    - `zh-CN`: Force Chinese
    - `en-US`: Force English
- `Projects`: Project configuration list
  - `ProjectName`: Project name
  - `Query`: Query configuration
    - `UseExistingQuery`: Whether to use existing query
    - `QueryPath`: Existing query path
    - `CustomWiql`: Custom WIQL query (optional)

## Generated Report Content

### Merged Report Mode

1. Overall Overview
   - Work item statistics for all projects
   - Completed and active item counts

2. Project Statistics
   - Work item statistics by project
   - Completion status by project

3. Overall Gantt Charts
   - Feature level view
   - User Story level view

4. Work Item Classification
   - Features list (with start and end dates)
   - User Stories list (with start date, end date, and parent Feature)
   - Tasks list (with start date, end date, and parent Story)

5. Team Member Task Assignment
   - Gantt chart for each member
   - Personal work item list (with start and end dates)

### Independent Report Mode

Each project generates an independent report containing:

1. Project Overview
   - Total work items
   - Completed items count
   - Active items count

2. Project Gantt Charts
   - Feature level view
   - User Story level view

3. Work Item Classification
   - User Stories (with start and end dates)
   - Tasks (with start date, end date, and parent Story)

4. Team Member Task Assignment
   - Personal Gantt chart
   - Personal work item list (with start and end dates)

## Status Mapping

Work item status mapping rules:
- Completed status (done): done, closed, completed, resolved, removed
- Active status (active): active, in progress, doing
- Other status: shown as normal tasks

## Dependencies

- .NET 8.0
- Microsoft.TeamFoundationServer.Client
- Microsoft.VisualStudio.Services.Client
- Newtonsoft.Json

## Notes

- Requires valid Azure DevOps PAT token
- PAT needs work item read permission
- Supported work item types: Feature, User Story, and Task
- Time display uses local timezone
- Merged report mode combines all project work items into one report
- Language set to auto will auto-detect system language
- 
- ## Sample Report
![work_items_test](media/work_items_test.png)