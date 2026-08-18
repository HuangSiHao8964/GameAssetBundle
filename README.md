# GameAssetBundle

`GameAssetBundle` 是项目内的 Unity AssetBundle 资源管线，包含资源采集、AssetBundle 构建、差异包生成、编辑器模拟以及运行时加载和释放能力。

包路径：`Client/LocalPackages/com.haofang.game-asset-bundle`

## 依赖与程序集配置

### UniTask 是必需依赖

运行时程序集 `GameAssetBundle`（`Runtime/GameAssetBundle.asmdef`）直接引用名为 `UniTask` 的程序集。包中的异步 API 也全部基于 UniTask，例如：

- `AssetBundleRuntimeContext` 的文件和 AssetBundle 读取委托；
- `AssetBundleManager.StartUp()`；
- `AssetBundleManager.LoadAllDependencies()`；
- `AssetManager.LoadAsync<T>()`；
- `AssetManager.InstantiateAsync()`。

`package.json` 没有声明 `UniTask` 依赖，因此导入本包不会自动安装或提供 UniTask。接入项目必须先确保 UniTask 已安装、已被 Unity 导入，并且程序集名称可以被 `GameAssetBundle.asmdef` 解析。业务代码调用这些 API 时通常还需要：

```csharp
using Cysharp.Threading.Tasks;
```

编辑器程序集 `GameAssetBundle.Editor` 引用运行时程序集，并且只在 Unity Editor 平台编译。

## 运行时接入

### 1. 注入宿主项目能力

包不负责具体的路径规划、文件下载、加密解密或网络重试。宿主项目必须通过 `AssetBundleRuntimeContext.Configure` 注入这些能力，然后才能启动资源管理器。

必须提供的配置包括：

- `AssetRecordsFileName`：资源记录文件名；
- `AssetBundleDifferenceFileName`：差异包映射文件名；
- `MainManifestPath`：主 Manifest 路径；
- `GetAssetFilePath`：按 `InitData`、`Local`、`Remote` 选择资源文件路径；
- `ReadFileBytes`：读取本地字节；
- `GetBytesAsync`：异步读取字节；
- `GetTextAsync`：异步读取文本；
- `GetAssetBundleAsync`：按路径异步加载 AssetBundle，并根据参数处理加密；
- `HasLocalManifest`：报告本地是否存在 Manifest。

日志委托 `Log`、`LogWarning`、`LogError` 可选，未提供时默认使用 Unity 的 `Debug.Log`、`Debug.LogWarning` 和 `Debug.LogError`。

### 2. 初始化顺序

建议在游戏启动阶段按以下顺序执行：

1. 确保 UniTask 可用；
2. 调用 `AssetBundleRuntimeContext.Configure(...)`；
3. 调用 `AssetManager.Init()` 清理上一次运行残留的租约和待加载句柄；
4. 等待 `AssetBundleManager.Instance.StartUp()` 完成；
5. 在 `StartUp()` 成功后调用 `AssetManager.LoadAsync<T>()` 或 `AssetManager.InstantiateAsync()`。

最小结构示例（路径和 IO 委托需要替换为项目实现）：

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameAssetBundle;
using UnityEngine;

public sealed class AssetBundleBootstrap : MonoBehaviour
{
    private async UniTask InitializeAsync()
    {
        AssetBundleRuntimeContext.Configure(new AssetBundleRuntimeConfig
        {
            AssetRecordsFileName = "ARecords.json",
            AssetBundleDifferenceFileName = "difference.json",
            MainManifestPath = "Windows.json",
            GetAssetFilePath = ResolveAssetPath,
            ReadFileBytes = ReadLocalBytes,
            GetBytesAsync = ReadBytesAsync,
            GetTextAsync = ReadTextAsync,
            GetAssetBundleAsync = LoadAssetBundleAsync,
            HasLocalManifest = HasLocalManifest,
        });

        AssetManager.Init();
        await AssetBundleManager.Instance.StartUp();
    }

    // 以下委托由宿主项目实现，示例仅展示签名。
    private string ResolveAssetPath(string fileName, AssetBundlePathType pathType) => throw new NotImplementedException();
    private byte[] ReadLocalBytes(string fileName, AssetBundlePathType pathType) => throw new NotImplementedException();
    private UniTask<byte[]> ReadBytesAsync(string path) => throw new NotImplementedException();
    private UniTask<string> ReadTextAsync(string path) => throw new NotImplementedException();
    private UniTask<AssetBundle> LoadAssetBundleAsync(string path, bool encrypted, CancellationToken token) => throw new NotImplementedException();
    private bool HasLocalManifest() => false;
}
```

`AssetBundleRuntimeContext` 未配置时，访问运行时配置会抛出异常；不要在配置前调用 `StartUp()` 或资源加载 API。

### 3. 本项目接入参考

当前 Battle 客户端的宿主适配位于：

- `Client/Assets/GameCore/AssetBundleRuntimeAdapter.cs`：将本地文件、远程请求和 AssetBundle 解密接入 `AssetBundleRuntimeConfig`；
- `Client/Assets/GameCore/GameRoot.cs`：在游戏初始化阶段配置 Context、调用 `AssetManager.Init()`，再启动 `AssetBundleManager`；
- `Client/Assets/HotUpdate/Common/StartUp.cs`：注册热更侧额外资源类型扩展，并使用 `InstantiateAsync` 创建运行时对象。

阅读或修改这些适配层时，应保持“先 Configure，再 Init，再 StartUp”的顺序，并保证宿主提供的 `AssetBundlePathType` 路径策略与资源发布目录一致。

## 加载、实例化与释放

### 加载普通资源

`AssetManager.LoadAsync<T>` 返回资源对象。返回的对象由 AssetManager 保留引用，使用完成后必须调用 `AssetManager.Release`。

```csharp
private async UniTask LoadMaterialAsync(string assetName)
{
    Material material = await AssetManager.LoadAsync<Material>(assetName);
    if (material == null)
        return;

    // 使用 material...
    AssetManager.Release(material);
}
```

### 实例化预制体

`InstantiateAsync` 会加载 `GameObject`、创建实例并建立实例租约。释放实例时调用 `AssetManager.Release(instance)`，该操作会销毁实例，并在没有其他实例使用时释放对应 AssetBundle 引用。

```csharp
private async UniTask<GameObject> SpawnAsync(string prefabName, Transform parent)
{
    GameObject instance = await AssetManager.InstantiateAsync(prefabName, parent);
    return instance;
}

private void Despawn(GameObject instance)
{
    AssetManager.Release(instance);
}
```

### 绑定到组件生命周期

带 `Component owner` 的 `LoadAsync<T>` 重载会把租约绑定到组件和 `slot`。同一组件的同一 slot 可以被后续加载替换，组件销毁时由 `AssetLeaseOwner` 负责释放。

```csharp
private async UniTask<Sprite> LoadIconAsync(Component owner, string assetName)
{
    return await AssetManager.LoadAsync<Sprite>(owner, assetName, "icon");
}
```

手动清理全部租约和待加载句柄时使用 `AssetManager.Clean()`。诊断信息可通过 `AssetManager.GetDiagnostics()` 获取，其中包含租约、待加载句柄和 AssetBundle 缓存状态。

## 资源名与记录

运行时加载使用资源名而不是直接使用物理文件名。`AssetName` 会根据资源类型和项目注册的扩展名生成标准资源名，并通过 `AssetBundleRecord` 找到资源所属 Bundle。

项目需要为会被加载的类型注册扩展名，例如 `GameObject` 对应 `prefab`、`TextAsset` 对应 `bytes/json/txt`、`AudioClip` 对应 `mp3/wav/ogg`。如果类型没有注册扩展，或资源记录中不存在生成后的资源名，`LoadAsync<T>` 会返回 `null`。

资源记录中包含：

- 资源名到 Bundle 索引的映射；
- Bundle 逻辑名与实际（通常为 MD5）文件名的映射；
- `IsEncrypted` 加密标记。

Bundle 依赖关系来自主 Manifest。`AssetBundleManager.StartUp()` 会先加载资源记录，再加载主 Manifest 和依赖表；加载具体资源时，管理器会按依赖关系维护 Bundle 引用。

## 编辑器工作流

所有编辑器菜单都位于 Unity 顶部菜单 `HaoFangTools/GameAssetBundle` 下。编辑器程序集为 `GameAssetBundle.Editor`，只在 Editor 平台编译，不应被运行时程序集反向引用。

### 配置

在 Unity 菜单中使用：

- `HaoFangTools/GameAssetBundle/Settings`：项目级文件名、扩展名和目录约定；
- `HaoFangTools/GameAssetBundle/Build Settings`：应用构建 Profile、目标平台、版本、加密和差异包基线；
- `HaoFangTools/GameAssetBundle/资源采集/配置窗口`：资源采集规则。

资源采集窗口维护一个有序规则列表。每条规则包含资源目录、打包方式和文件类型；目录无效时窗口会标红，规则顺序会影响采集结果。窗口内的 `添加规则`、`删除规则`、`保存`、`清空` 和 `定位资产` 操作都会直接修改包内的 `AssetBundleCollectConfig.asset`。

默认设置资产位于包的 `Resources/setting.asset`。运行时通过 `Resources.Load("setting")` 读取设置，因此不要随意移动或重命名该资产，除非同时调整代码约定。

### 构建

在 `HaoFangTools/GameAssetBundle/Build` 下可执行：

- `仅导出资源`：根据当前 Profile 构建资源并记录构建产物；
- `导出资源并生成差异包`：构建当前版本并与 Profile 中的基线版本生成差异包；
- `仅生成差异包`：使用已有构建产物生成差异包；
- `Build AB Names`、`Clear AB Names`：维护 AssetBundle 名称；
- `Test`：执行构建侧测试入口。

构建设置中的 Application Build Profile 是构建操作的边界：它决定目标平台、ScriptingImplementation、Release 模式、是否清理旧包、是否递增资源版本、基线版本和当前版本。构建前应先在 `Build Settings` Inspector 中选择 `Active Application Build`，并确保 Profile 名称非空且不重复。

代码调用时可以直接使用 `GameAssetBundle.Edit.AssetBundleBuildActions`：

```csharp
AssetBundleBuildResult result = AssetBundleBuildActions.BuildResources("Default");
AssetBundleDifferencePackage difference =
    AssetBundleBuildActions.BuildResourcesAndDifference("Default");
```

构建流程会创建并保存构建产物清单，写入版本文件；开启加密时还会转换生成的 Bundle。差异包生成要求基线产物和当前产物属于同一应用 Profile、目标平台和加密模式，且两个版本不能相同。

启用 Profile 的 AssetBundle 加密时，宿主必须先通过 `AssetBundleBuildActions.RegisterEncryptionCallback(...)` 注册加密回调，否则构建会失败。

构建前后回调可通过 `RegisterBuildHooks` 注册：前置回调用于生成热更包装或切换构建环境，后置回调用于处理构建目录。回调属于进程内编辑器状态，重新打开 Unity 后需要重新注册。

### AssetBundle 名称维护

- `Build AB Names`：根据资源路径为导入器写入 AssetBundle 名称；
- `Clear AB Names`：移除资源上的 AssetBundle 名称；
- `Test`：执行构建器提供的测试入口。

名称维护会修改 Unity 资源导入设置，执行前请确认当前选择的资源范围和目标平台。

### 编辑器模拟

`AssetBundleOption.SimulateAssetBundleInEditor` 控制编辑器是否模拟 AssetBundle 加载。默认值来自 Unity `EditorPrefs`，默认开启；这样可以避免每次资源修改后都重新打包。相关菜单位于：

`HaoFangTools/GameAssetBundle/Simulation/模拟AssetBundles`

运行时构建不会使用该编辑器模拟开关。

### 校验与刷新

`HaoFangTools/GameAssetBundle/Simulation/Reload Editor Resources` 可刷新编辑器资源记录。`HaoFangTools/GameAssetBundle/Validation` 下提供 Manifest 循环依赖检查和资源文件名合法性检查。

校验工具的用途如下：

- `Check Loop By Manifest`：读取主 Manifest，遍历 Bundle 直接依赖，发现循环依赖时抛出包含路径的错误；
- `检查资源文件名是否合法`：检查 `Assets/ABR`（或 `AssetBundleSettings.LocalAbrPath` 指定目录）下的文件名，报告包含空格或中文的路径；
- `Reload Editor Resources`：重新加载采集数据，适合修改资源或采集规则后刷新编辑器缓存。

这些菜单依赖已生成的本地资源和 Manifest；在尚未构建资源时执行依赖循环检查可能会因为缺少 Manifest 而失败。

## 关键目录

```text
com.haofang.game-asset-bundle/
├── package.json
├── Runtime/
│   ├── Core/          # AssetBundleManager、ABInfo、记录、路径和文件工具
│   ├── Handles/       # AssetManager、AssetHandle、租约和释放逻辑
│   ├── Integration/   # AssetBundleRuntimeContext 与宿主注入接口
│   ├── Naming/        # 资源名和扩展名规则
│   └── Settings/      # 运行时设置和编辑器模拟选项
├── Editor/
│   ├── Build/         # 采集、构建、构建产物和差异包
│   ├── Settings/      # 构建设置和采集配置 Inspector
│   └── Simulation/    # 编辑器模拟和校验菜单
└── Resources/
    └── setting.asset  # 默认运行时设置
```

## 常见问题

### `GameAssetBundle.asmdef` 找不到 `UniTask`

确认项目已安装 UniTask，并且 UniTask 的程序集名称是 `UniTask`。仅在 `package.json` 中看到 `GameAssetBundle` 不代表 UniTask 会被自动解析；必要时检查 UniTask 包的 asmdef、程序集平台设置以及编译定义。

### 启动时报 `AssetBundleRuntimeContext is not configured`

在任何 `StartUp()`、`LoadAsync<T>()` 或 `InstantiateAsync()` 调用前执行 `AssetBundleRuntimeContext.Configure(...)`，并确认所有必需委托都不为 `null`。

### 资源加载返回 `null`

依次检查：资源记录是否已加载、传入资源名是否符合 `AssetName` 规则、目标类型是否正确、宿主路径委托是否返回了正确文件，以及主 Manifest 和依赖 Bundle 是否可用。

### 资源或实例何时释放

普通资源在 `AssetManager.Release(asset)` 后释放；实例在 `AssetManager.Release(instance)` 后销毁。若使用带 owner/slot 的重载，应通过 `AssetManager.Release(owner, slot)` 或 owner 的销毁流程释放对应租约。

## 相关入口

- 运行时：`GameAssetBundle.AssetBundleRuntimeContext`
- 运行时：`GameAssetBundle.AssetBundleManager`
- 运行时：`GameAssetBundle.AssetManager`
- 编辑器：`GameAssetBundle.Edit.AssetBundleBuildActions`
- 编辑器：`GameAssetBundle.Edit.AssetBundleBuildSetting`
