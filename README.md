# Azure2Md

[English](README.en.md) | 简体中文

一个将 Azure DevOps 工作项导出为 Markdown 报告的工具。

## 功能特点

- 支持多个项目的工作项导出
- 支持项目报告的合并或独立生成
- 支持完整的工作项层级关系（Feature -> User Story -> Task）
- 生成多层级甘特图视图
  - Feature + User Story 视图
  - User Story + Task 视图
- 按人员分组显示任务
- 支持自定义查询或使用现有查询
- 显示工作项的完整时间信息
- 支持任务状态的可视化（进行中/已完成）
- 支持多语言（中文/英文/自动检测）
- 支持自定义甘特图显示选项

## 配置说明

在 `appsettings.json` 中配置：

```json
{
    "TfsUrl": "https://dev.azure.com/你的组织名",
    "PersonalAccessToken": "你的PAT令牌",
    "ReportSettings": {
        "MergeProjects": true,
        "MergedTitle": "多项目整合报告",
        "Language": "auto",  // 可选值：auto, zh-CN, en-US
        "DisplayOptions": {
            "ShowFeatureInGantt": true,  // 是否在甘特图中显示 Feature
            "ShowUserStoryInGantt": true,  // 是否在甘特图中显示 User Story
            "PrefixParentName": true  // 是否在子项前显示父项名称
        }
    },
    "Projects": [
        {
            "ProjectName": "项目名称",
            "Query": {
                "UseExistingQuery": false,
                "QueryPath": "Shared Queries/查询路径",
                "CustomWiql": null
            }
        }
    ]
}
```

### 配置项说明

- `TfsUrl`: Azure DevOps 组织的 URL
- `PersonalAccessToken`: 个人访问令牌（PAT）
- `ReportSettings`: 报告生成设置
  - `MergeProjects`: 是否合并多个项目的报告
  - `MergedTitle`: 合并报告的标题
  - `Language`: 语言设置
    - `auto`: 自动检测系统语言
    - `zh-CN`: 强制使用中文
    - `en-US`: 强制使用英文
  - `DisplayOptions`: 显示选项
    - `ShowFeatureInGantt`: 是否在甘特图中显示 Feature 节点
    - `ShowUserStoryInGantt`: 是否在甘特图中显示 User Story 节点
    - `PrefixParentName`: 是否在子项前添加父项名称
- `Projects`: 项目配置列表
  - `ProjectName`: 项目名称
  - `Query`: 查询配置
    - `UseExistingQuery`: 是否使用现有查询
    - `QueryPath`: 现有查询的路径
    - `CustomWiql`: 自定义 WIQL 查询（可选）

## 甘特图显示说明

1. 父子项显示
   - 当 `ShowFeatureInGantt` 为 true 时，显示 Feature 节点
   - 当 `ShowUserStoryInGantt` 为 true 时，显示 User Story 节点
   - 子项始终显示

2. 名称显示
   - 当 `PrefixParentName` 为 true 时：
     - User Story 显示为：`Feature名称 - User Story名称`
     - Task 显示为：`User Story名称 - Task名称`
   - 当 `PrefixParentName` 为 false 时，仅显示项目自身名称

3. 节点类型标识
   - Feature 节点显示为：`名称 (Feature)`
   - User Story 节点显示为：`名称 (Story)`
   - Task 节点不添加类型标识

## 生成的报告内容

### 合并报告模式

1. 总体概览
   - 所有项目的工作项统计
   - 完成项和进行中项的数量

2. 项目统计
   - 各项目的工作项统计
   - 各项目的完成情况

3. 总体甘特图
   - Feature 层级视图
   - User Story 层级视图

4. 工作项分类
   - Features 列表（包含开始日期和结束日期）
   - User Stories 列表（包含开始日期、结束日期和所属 Feature）
   - Tasks 列表（包含开始日期、结束日期和所属 Story）

5. 团队成员任务分配
   - 每个成员的甘特图
   - 个人工作项列表（包含开始日期和结束日期）

### 独立报告模式

每个项目生成独立的报告，包含：

1. 项目概览
   - 工作项总数
   - 完成项数量
   - 进行中数量

2. 项目甘特图
   - Feature 层级视图
   - User Story 层级视图

3. 工作项分类
   - User Stories（包含开始日期和结束日期）
   - Tasks（包含开始日期、结束日期和所属 Story）

4. 团队成员任务分配
   - 个人甘特图
   - 个人工作项列表（包含开始日期和结束日期）

## 状态映射

工作项状态映射规则：
- 完成状态（done）：done, closed, completed, resolved, removed
- 进行中状态（active）：active, in progress, doing
- 其他状态：显示为普通任务

## 依赖项

- .NET 8.0
- Microsoft.TeamFoundationServer.Client
- Microsoft.VisualStudio.Services.Client
- Newtonsoft.Json

## 注意事项

- 需要有效的 Azure DevOps PAT 令牌
- PAT 需要有读取工作项的权限
- 支持的工作项类型：Feature、User Story 和 Task
- 时间显示采用本地时区
- 合并报告模式下会将所有项目的工作项整合到一个报告中
- 语言设置为 auto 时会自动检测系统语言

## 示例报告
![work_items_test](media/work_items_test.png)