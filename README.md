# Azure2Md
从 Azure 中获取数据，生成 Markdown 文件报告

## 功能介绍

Azure2Md 是一个用于从 Azure DevOps 中获取工作项数据并生成 Markdown 格式报告的工具。

主要功能包括:

- 通过 Azure DevOps REST API 获取工作项数据
- 支持使用现有查询或自定义 WIQL 查询
- 生成包含以下内容的 Markdown 报告:
  - 项目整体甘特图
  - 工作项总体列表(包含 ID、标题、状态、负责人)
  - 按人员分组的任务视图(每人的甘特图和工作项列表)

## 配置说明

# Start of Selection
在 `appsettings.json` 中，您需要配置以下内容：
{
  "TfsUrl": "您的 TFS URL",
  "ProjectName": "您的项目名称",
  "PersonalAccessToken": "您的个人访问令牌",
  "Query": {
    "UseExistingQuery": true,
    "QueryPath": "现有查询路径",
    "CustomWiql": "自定义 WIQL 查询"
  }
}




