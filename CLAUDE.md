# AGENTS.md — RimMind-Memory

本文件供 AI 编码助手阅读，描述 RimMind-Memory 的架构、代码约定和扩展模式。

## 项目定位

RimMind-Memory 是 RimMind AI 模组套件的记忆系统模块。职责：

1. **记忆采集**：监听游戏事件（工作、受伤、死亡、技能升级等），生成记忆条目
2. **工作会话聚合**：将连续同类工作聚合成单条记忆（如"搬运 x12 约4.8游戏时"）
3. **叙事者记忆**：采集殖民地级别事件（袭击、事件等），形成叙事视角
4. **暗记忆生成**：每日调用 AI 将当日记忆凝练为长期印象（<=50字）
5. **上下文注入**：通过 Provider 注册机制将记忆注入 AI Prompt
6. **重要度衰减**：可选机制，低重要度记忆随时间衰减直至移除
7. **公开 API**：`RimMindMemoryAPI.AddMemory()` 供其他模组（如 Dialogue）写入记忆

## 源码结构

```
Source/
├── RimMindMemoryMod.cs           Mod 入口，Harmony 补丁，设置 UI
├── RimMindMemoryAPI.cs           公开静态 API，供外部模组添加记忆
├── Settings/
│   └── RimMindMemorySettings.cs  模组设置（触发器、容量、衰减等）
├── Data/
│   ├── MemoryEntry.cs            记忆条目数据结构
│   ├── PawnMemoryStore.cs        单个 Pawn 的记忆存储（活跃/存档/暗记忆）
│   ├── NarratorMemoryStore.cs    叙事者记忆存储
│   └── RimMindMemoryWorldComponent.cs  WorldComponent，管理所有存储
├── Injection/
│   └── MemoryContextProvider.cs  向 RimMind-Core 注册上下文 Provider
├── Aggregation/
│   ├── WorkSessionAggregator.cs  工作会话聚合逻辑（GameComponent）
│   └── Patch_StartJob_Memory.cs  监听 Job 开始，触发聚合
├── Triggers/
│   ├── Patch_PawnKill.cs         死亡事件
│   ├── Patch_AddHediff.cs        受伤/患病
│   ├── Patch_MentalBreak.cs      精神崩溃
│   ├── Patch_SkillLevelUp.cs     技能升级
│   └── Patch_AddRelation.cs      关系建立
├── Narrator/
│   └── Patch_IncidentWorker.cs   叙事者事件采集
├── DarkMemory/
│   └── DarkMemoryUpdater.cs      每日暗记忆生成（GameComponent）
├── Decay/
│   └── ImportanceDecayManager.cs 重要度衰减管理
├── Core/
│   ├── TimeFormatter.cs          时间格式化（相对时间/游戏日期）
│   └── ImportanceDecayCalculator.cs 衰减计算
├── UI/
│   ├── Dialog_MemoryLog.cs       记忆日志窗口（Pawn 详情页弹出）
│   ├── Dialog_InputMemory.cs     手动添加记忆输入窗口
│   ├── BioTabMemoryPatch.cs      在 Bio 页添加"记忆"按钮
│   └── NarratorSettingsTab.cs    叙事者设置页
└── Debug/
    └── MemoryDebugActions.cs     Dev 菜单调试动作
```

## 关键类与数据结构

### RimMindMemoryAPI

公开 API，供其他模组写入记忆：

```csharp
public static class RimMindMemoryAPI
{
    static bool AddMemory(string content, string memoryType, int tick, float importance, string? pawnId = null);
    // 返回 false 表示 pawn 未找到或 store 为空
}
```

### MemoryEntry

```csharp
public enum MemoryType { Work, Event, Manual, Dark }

public class MemoryEntry : IExposable
{
    public string id;           // 唯一标识 mem-{tick}
    public string content;      // 记忆内容（中文）
    public MemoryType type;     // 类型
    public int tick;            // 发生时间（游戏 tick）
    public float importance;    // 重要度 0-1
    public bool isPinned;       // 是否固定（不被淘汰/衰减）
    public string? pawnId;      // 关联 Pawn（叙事者记忆用）
    public string? notes;       // 备注

    static MemoryEntry Create(string content, MemoryType type, int tick, float importance, string? pawnId = null);
    // Dark 类型自动 isPinned=true
}
```

### PawnMemoryStore / NarratorMemoryStore

三层存储结构：
- **active**：活跃记忆，按时间倒序，有容量上限
- **archive**：存档记忆，按重要度排序，容量满时淘汰低重要度条目
- **dark**：暗记忆/长期印象，AI 生成，只读展示

```csharp
public class PawnMemoryStore : IExposable
{
    public List<MemoryEntry> active;
    public List<MemoryEntry> archive;
    public List<MemoryEntry> dark;

    public void AddActive(MemoryEntry e, int maxActive, int maxArchive);
    public static void EnforceLimit(List<MemoryEntry> src, int srcMax, List<MemoryEntry> dst, int dstMax);
    // 溢出时按 importance 排序移入 dst，dst 溢出则丢弃最低优先级非 pinned 条目
    public bool IsEmpty { get; }
}
```

### 存储层级

```
WorldComponent (RimMindMemoryWorldComponent)
    ├── Dictionary<int, PawnMemoryStore> _pawnStores  // 每个 Pawn 的存储
    └── NarratorMemoryStore _narratorStore            // 叙事者存储
```

## 记忆触发来源

| 来源 | 触发器 | 重要度计算 |
|------|--------|-----------|
| 工作会话 | Patch_StartJob_Memory -> WorkSessionAggregator | 时长>15000tick=0.5，否则0.4 |
| 重要工作 | WorkSessionAggregator 白名单任务 | 0.5-0.9（Rescue=0.8, Attack=0.9） |
| 受伤/患病 | Patch_AddHediff | lethal=0.9, chronic=0.8, tendable=0.7, sick=0.6 |
| 精神崩溃 | Patch_MentalBreak | Berserk=0.95, Extreme=0.9, Serious=0.8, 其他0.7 |
| 亲近者死亡 | Patch_PawnKill | 有关系=1.0，无关系=0.85 |
| 技能升级 | Patch_SkillLevelUp | >=15级=0.7，否则0.5 |
| 关系建立 | Patch_AddRelation | Spouse/Lover=0.95, Parent/Child=0.9, Sibling/Bond=0.85 |
| 叙事者事件 | Patch_IncidentWorker | 0.3-1.0（Raid=0.9, Wedding=0.85） |
| 手动添加 | Dialog_InputMemory 按钮 | 固定 0.6 |
| 外部模组 | RimMindMemoryAPI.AddMemory | 调用方指定 |

## 工作会话聚合逻辑

WorkSessionAggregator 维护每个 Pawn 的当前会话：

```csharp
class PawnSession
{
    string? currentJobDef;
    int startTick;
    int lastJobTick;
    int count;
    int totalTicks;
}
```

会话结束条件：
1. 工作类型切换
2. 单次会话超时（2500 ticks）
3. 空闲间隔超过阈值（默认 6000 ticks）触发"休息/待机"记忆

高重要度工作（Rescue、Attack 等）直接单独记录，不聚合。

## 暗记忆生成

DarkMemoryUpdater（GameComponent）每日执行：

1. **Pawn 暗记忆**：每个殖民者独立请求 AI
   - 输入：今日记忆 + 现有暗记忆
   - 输出：JSON `{"dark": ["...", ...]}`（固定条数，每条<=50字）
   - 存储：替换 store.dark

2. **叙事者暗记忆**：殖民地级别
   - 输入：今日叙事事件 + 现有暗叙事
   - 输出：同上格式
   - 存储：替换 narratorStore.dark

## 上下文注入

MemoryContextProvider 注册两个 Provider：

```csharp
// Pawn 级别记忆（通过 RegisterPawnContextProvider，优先级 PriorityMemory）
RimMindAPI.RegisterPawnContextProvider("memory_pawn", pawn => {
    // 返回格式：
    // [PawnName 近期记忆]
    // - 约2h前：搬运 x12 约4.8游戏时
    // [PawnName 存档记忆（按重要度）]
    // - 1天前：患上流感（全身）
    // [PawnName 长期印象（AI生成）]
    // - 最近频繁生病，身体变差
}, PromptSection.PriorityMemory);

// 叙事者记忆（通过 RegisterStaticProvider，优先级 PriorityAuxiliary）
RimMindAPI.RegisterStaticProvider("memory_narrator", () => {
    // 返回格式：
    // [殖民地叙事记忆]
    // - 约1h前：[地图] 袭击（来自海盗团）
    // [存档叙事（按重要度）]
    // - 2天前：殖民者艾丽斯死亡
    // [长期叙事（AI生成）]
    // - 殖民地近期遭受多次袭击
}, PromptSection.PriorityAuxiliary);
```

注入比例由设置控制（activeInjectRatio / archiveInjectRatio）。

## 重要度衰减

可选机制（默认关闭）：

```csharp
// 每日执行
ImportanceDecayManager.ApplyDecay(store, decayRate, minThreshold);

// 衰减公式
importance = importance * (1 - decayRate)

// 移除条件
if (importance < minThreshold && !isPinned) remove from archive
```

## 代码约定

### 命名空间

- `RimMind.Memory` — Mod 入口、Settings、公开 API
- `RimMind.Memory.Data` — 数据结构和存储
- `RimMind.Memory.Injection` — 上下文注入
- `RimMind.Memory.Aggregation` — 工作会话聚合
- `RimMind.Memory.Triggers` — 事件触发器
- `RimMind.Memory.Narrator` — 叙事者事件
- `RimMind.Memory.DarkMemory` — 暗记忆生成
- `RimMind.Memory.Decay` — 衰减管理
- `RimMind.Memory.Core` — 工具类
- `RimMind.Memory.UI` — 界面
- `RimMind.Memory.Debug` — 调试动作

### Harmony 补丁

- Harmony ID：`mcocdaa.RimMindMemory`
- 优先使用 Postfix
- 所有 Patch 类放在 `Triggers/` 或 `Aggregation/` 目录

### 错误处理

所有触发器使用 try-catch 包裹，避免影响游戏：

```csharp
try
{
    // 记忆采集逻辑
}
catch (Exception ex)
{
    Log.Warning($"[RimMind-Memory] Patch_XXX error: {ex.Message}");
}
```

### 设置项默认值

```csharp
maxActive = 30;           // 小人活跃记忆上限
maxArchive = 50;          // 小人存档记忆上限
darkCount = 3;            // 小人暗记忆条数
narratorMaxActive = 30;   // 叙事者活跃上限
narratorMaxArchive = 10;  // 叙事者存档上限
narratorDarkCount = 10;   // 叙事者暗叙事条数
activeInjectRatio = 0.5f; // 活跃记忆注入比例
archiveInjectRatio = 0.5f;// 存档记忆注入比例
narratorActiveInjectRatio = 0.5f;
narratorArchiveInjectRatio = 0.5f;
enableDecay = false;      // 衰减开关
decayRate = 0.02;         // 衰减率
minImportanceThreshold = 0.05; // 最低重要度阈值
minAggregationCount = 2;  // 聚合最低次数
idleGapThresholdTicks = 6000; // 空闲间隔阈值
narratorEventThreshold = 0.2; // 叙事者事件阈值
pawnToNarratorThreshold = 0.8; // Pawn→叙事者升级阈值
requestExpireTicks = 30000; // 请求过期
```

## 扩展指南

### 添加新的记忆触发器

1. 在 `Triggers/` 创建新的 Patch 类
2. 监听目标事件，构造 MemoryEntry
3. 调用 `store.AddActive()` 写入
4. 如重要度足够，同步写入 NarratorStore

示例模板：

```csharp
[HarmonyPatch(typeof(TargetType), "TargetMethod")]
public static class Patch_MyTrigger
{
    static void Postfix(TargetType __instance)
    {
        if (!RimMindMemoryMod.Settings.enableMemory) return;
        if (!RimMindMemoryMod.Settings.triggerMyFeature) return;

        try
        {
            var wc = RimMindMemoryWorldComponent.Instance;
            if (wc == null) return;

            var settings = RimMindMemoryMod.Settings;
            int now = Find.TickManager.TicksGame;
            float importance = 0.7f;
            string content = "事件描述";

            var store = wc.GetOrCreatePawnStore(pawn);
            store.AddActive(MemoryEntry.Create(content, MemoryType.Event, now, importance),
                settings.maxActive, settings.maxArchive);
        }
        catch (Exception ex)
        {
            Log.Warning($"[RimMind-Memory] Patch_MyTrigger error: {ex.Message}");
        }
    }
}
```

### 通过 API 添加记忆（外部模组）

```csharp
RimMindMemoryAPI.AddMemory("与艾丽斯进行了深度交谈", "Dialogue", Find.TickManager.TicksGame, 0.6f, pawnId: pawn.ThingID.ToString());
```

### 添加新的工作类型到聚合

在 `WorkSessionAggregator.WorkJobLabels` 添加映射：

```csharp
{ "MyNewJob", "新工作中文标签" }
```

如该工作应单独记录（非聚合），添加到 `SignificantJobs` 和 `SignificantJobLabels`。

## 依赖关系

```
RimMind-Memory
    ├── RimMind-Core（API、上下文注入）
    ├── Harmony
    └── RimWorld 1.6
```

RimMind-Memory 不依赖其他 RimMind 子模组（Personality、Actions、Advisor 等），但会向它们提供记忆上下文。其他模组可通过 `RimMindMemoryAPI.AddMemory` 写入记忆。

## 测试

- 单元测试项目：`Tests/`，使用 xUnit，目标 `net10.0`
- 已有测试：`PawnMemoryStoreTests`、`ImportanceDecayTests`、`TimeFormatterTests`
- `VerseStubs.cs` 提供 RimWorld 类型桩用于测试
