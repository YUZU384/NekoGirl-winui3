# NekoGirl - 猫娘图片浏览器

<p align="center">
  <img src="NekoGirl-winui3/Assets/Neko150x150Logo.png" alt="NekoGirl Logo" width="120">
</p>

<p align="center">
  <b>🐱 一款优雅的 WinUI 3 猫娘图片浏览与下载工具</b>
</p>

<p align="center">
  <a href="界面预览">界面预览</a> •
  <a href="#功能特性">功能特性</a> •
  <a href="#系统要求">系统要求</a> •
  <a href="#安装与使用">安装与使用</a> •
  <a href="#键盘快捷键">键盘快捷键</a> •
  <a href="#技术栈">技术栈</a> •
  <a href="#开源协议">开源协议</a>
</p>

---

## 🔍 界面预览



## 📖 项目简介

**NekoGirl** 是一款基于 Windows App SDK (WinUI 3) 开发的桌面应用程序，用于浏览和下载来自 [nekos.best](https://nekos.best) API 的猫娘图片。应用采用现代化的 Fluent Design 设计语言，提供流畅的用户体验和美观的界面。

这是 NekoGirl 的 WinUI 3 重制版，相比旧版本拥有更好的性能、更现代的 UI 和更流畅的动画效果。

## ✨ 功能特性

- 🖼️ **图片浏览** - 随机获取并浏览精美的猫娘图片
- 💾 **一键保存** - 快速保存喜欢的图片到本地
- 🎨 **现代化 UI** - 采用 WinUI 3 和 Fluent Design 设计
- ⌨️ **键盘导航** - 支持方向键和空格键快速浏览
- 👨‍🎨 **画师信息** - 显示图片作者信息并支持跳转主页
- 📁 **自定义保存路径** - 可自由选择图片保存位置
- 🔄 **预加载机制** - 智能预加载下批图片，浏览更流畅
- 🌈 **流畅动画** - 精心设计的过渡动画效果

## 💻 系统要求

| 项目       | 要求                                     |
| -------- | -------------------------------------- |
| **操作系统** | Windows 10 版本 1809 (Build 17763) 或更高版本 |
| **推荐系统** | Windows 11                             |
| **运行时**  | .NET 10 或更高版本                          |
| **架构**   | x86 / x64 / ARM64                      |

## 🚀 安装与使用

### 从源码构建

1. 克隆仓库
   
   ```bash
   git clone https://github.com/yourusername/NekoGirl-winui3.git
   cd NekoGirl-winui3
   ```

2. 使用 Visual Studio 2022 或更高版本打开 `NekoGirl-winui3.sln`

3. 确保已安装以下工作负载：
   
   - .NET 桌面开发
   - 通用 Windows 平台开发
   - Windows App SDK

4. 按 `F5` 或点击「开始调试」运行项目

### 发布应用

```bash
# 发布 x64 版本
dotnet publish -c Release -r win-x64 --self-contained true

# 发布 ARM64 版本
dotnet publish -c Release -r win-arm64 --self-contained true
```

## ⌨️ 键盘快捷键

| 快捷键           | 功能     |
| ------------- | ------ |
| `←` (左方向键)    | 上一张图片  |
| `→` (右方向键)    | 下一张图片  |
| `Space` (空格键) | 下一张图片  |
| `Ctrl + S`    | 保存当前图片 |

## 🛠️ 技术栈

- **UI 框架**: [Windows App SDK 1.8](https://learn.microsoft.com/windows/apps/windows-app-sdk/) (WinUI 3)
- **目标框架**: .NET 10
- **开发语言**: C# 12
- **API 数据源**: [nekos.best](https://nekos.best)
- **设计系统**: Fluent Design System

## 📁 项目结构

```
NekoGirl-winui3/
├── NekoGirl-winui3/
│   ├── Assets/                 # 应用图标和资源
│   ├── Services/
│   │   └── GetImageService.cs  # 图片获取服务
│   ├── MainWindow.xaml         # 主窗口 XAML
│   ├── MainWindow.xaml.cs      # 主窗口逻辑
│   ├── App.xaml                # 应用资源
│   └── App.xaml.cs             # 应用入口
└── README.md
```

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

1. Fork 本仓库
2. 创建你的功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交你的更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开一个 Pull Request

## 📜 开源协议

本项目采用 [MIT协议](..\LICENSE) 开源。

## 🙏 致谢

- 图片数据由 [nekos.best](https://nekos.best) API 提供
- 感谢所有画师创作的精美作品
- 感谢 WinUI 3 和 Windows App SDK 团队

---

<p align="center">
  Made with 💖 and 🐱
</p>
