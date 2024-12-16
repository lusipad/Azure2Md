# Azure2Md

一个将 Azure DevOps 工作项导出为 Markdown 报告的工具。

## 功能特点

- 支持多个项目的工作项导出
- 生成包含甘特图的 Markdown 报告
- 支持 User Story 和 Task 的层级关系
- 按人员分组显示任务
- 支持自定义查询或使用现有查询
- 显示工作项的完整时间信息
- 支持任务状态的可视化（进行中/已完成）

## 配置说明

在 `appsettings.json` 中配置：
``` json
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
```



