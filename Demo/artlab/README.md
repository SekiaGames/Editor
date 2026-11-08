# 美术资源验证工程

把地图里所有可显示物拆成一个个测试单元摆出来，用来定位「资源转换 / 材质 / 渲染」哪一环出错。

两类展台，各自「一个场景 = 一组」：

| 展台 | 一个场景 = | 命名 | 数量 |
|---|---|---|---|
| `terrain/tiles/` | 一个地表测试点（3×3 格） | `mask_01_br.tscn`、`ring_ne.tscn` | 35 |
| `units/` | 一组风格单位 | `unit_human.tscn`、`creep_bandit.tscn` | 55 |

---

# 一、地表 `terrain/tiles/`

**一个场景 = 一个测试点 = 3×3 格**，文件名就是测试点名。

为什么是 3×3：`w3e` 的角点被 4 个格共用，中心格的四角正好是整块 4×4 角点里的中间四个，
所以「中心格的四角组合」和「相邻格的过渡」在同一块区域里能同时看到；再小表达不了，再大只是重复。

## 目录

```
Demo/artlab/terrain/tiles/
├── pure_dirt.tscn                ← 一个文件一个测试点
├── mask_01_br.tscn
├── ...
└── shared/
    ├── ground_atlas.res          地表图集（129 层，所有场景共用）
    └── ground_material.tres      Demo shader（图集版）
```

35 个 tscn 各约 8 KB，共享图集 2.7 MB，整目录 3.0 MB。

每个场景 5 个节点、0 脚本：根 `Node3D` + `MapTerrain` + `Title`(Label3D) + `UnitFrame`(白框 3×3) + `CellFrame`(黄框 1×1)。
根节点 meta：`map_width/map_height = 3`、`view_dist = 640`、`view_target = (-192, 0, 192)` —— 打开就能看到合适的取景。

## 重建

```bash
dotnet build Godot/WorldEdit.csproj
"<Godot>" --path Godot -- --art-lab-terrain=res://Demo/artlab/terrain/tiles
```

必须**带渲染器**跑（不能 `--headless`）：图集要从 `Texture2DArray` 取像素，headless 的 dummy 渲染器返回空图层，
最后材质里没有贴图。生成完要让 Godot 导入一次（编辑器打开或 `--editor --quit`），否则 `.tscn` 引用的 `.res` 还没注册。

## 单元清单（35 个）

| 文件名 | 内容 |
|---|---|
| `pure_dirt` / `pure_grass` | 对照：整块同一种地表，检查纹理变体与无缝 |
| `mask_00_none` … `mask_15_all` | 中心格四角的 16 种组合（bit0=右下、bit1=左下、bit2=右上、bit3=左上） |
| `ring_e_px` / `ring_w_mx` / `ring_n_py` / `ring_s_my` / `ring_ne` / `ring_nw` / `ring_se` / `ring_sw` | 中心格 + 单方向一格泥土（两块泥土的边界） |
| `block_plus` / `block_diamond` / `block_2x2_corner` / `block_line3` | 泥土块形状：十字、菱形、2×2、3 格长条 |
| `tri_3dirt_1rock` / `tri_3rock_1dirt` | 三张地表混铺（4 个图层全用上） |
| `quad_4grounds` | 四角四张不同地表（层数最多） |
| `variation_step` / `variation_quad` | 相邻角点不同 variant（0/8/16/24），验证底层只取左下角 |

## 观察要点

- **单角泥土呈菱形**：一个角点是泥土时，它被 4 个相邻格共享，每格各取 1/4，
  拼成以该角点为中心、直径约 1 格的菱形。这是 War3 的角点语义，不是圆形画笔，也不是「占 1/4 格」。
- **mask 0 / 15 是纯色**：mask 0 = 中心四角全草地（与陪衬同色，看不到过渡），mask 15 = 中心整格泥土。
- **底层永远是四角里编号最小的地表**：底色由它决定，所以同一组角点换顺序会出现不同观感。
- **场景边界是硬切**：3×3 就是全部，边缘格的过渡被地图边界截断，属于正常现象。

## 已实测的结论

地表贴图 `512×256` = 8 列 × 4 行 = 32 个 64×64 小块：

| 块号 | 用途 |
|---|---|
| 0–15 | 遮罩块（alpha 渐变，按四角 mask 索引） |
| 16–31 | 纯色块（纹理变体，variation 0–15 映射到 16–31） |

把 `Lords_Dirt.png` 的 0–15 号块按四象限平均 alpha 量出来（**alpha 高 = 该地表的覆盖区**）：

| mask | TL | TR | BL | BR | 覆盖方向 |
|---|---|---|---|---|---|
| 1 | 7 | 108 | 87 | 255 | 右下 |
| 2 | 108 | 42 | 255 | 126 | 左下 |
| 4 | 138 | 255 | 9 | 94 | 右上 |
| 8 | 255 | 128 | 135 | 9 | 左上 |
| 3 | 173 | 176 | 255 | 255 | 底边 |
| 5 | 187 | 255 | 171 | 255 | 左边 |
| 10 | 255 | 219 | 255 | 208 | 右边 |
| 12 | 255 | 255 | 161 | 182 | 底边 |
| 0 / 15 | 255 | 255 | 255 | 255 | 无过渡（全覆） |

结论：**位序是 bit0=右下、bit1=左下、bit2=右上、bit3=左上**，与 `ViewportPanel.TileLayers` 一致。
（早前把「alpha 低」当成覆盖，才误判成「位序反了」，这里记一笔免得再犯。）

图集层号固定：`0`=全透明空层、`Ldrt` 1/17、`Lrok` 33/49、`Lgrs` 65/81、`Lgrd` 97/113（前半遮罩区，后半纯色区 +16）。
所有场景共用同一份图集，所以层号可以横向对比。

## 截图

```bash
"<Godot>" --path Godot -- --scene=res://Demo/artlab/terrain/tiles/mask_01_br.tscn \
  --pitch=-89 --no-brush --shot=test/shots/lab_mask01.png --frames=30
```

`--no-brush` 关掉笔刷光圈（它固定在地图 (0,0) 格附近，会压在 3×3 单元上）。
`--pitch=-89` 正俯视：看地表形态必须正俯视，斜视角会把菱形看成椭圆。

---

# 二、单位 `units/`

**一个场景 = 一组风格单位**，文件名即组名。每组 4～80 个模型，网格排布（8 列，格距 380），
每个单位头顶挂 `Label3D` 写 `unitID + unitClass`。

## 重建

```bash
"<Godot>" --path Godot -- --art-lab-units=res://Demo/artlab/units
```

## 分组规则（全部来自表，不是人工清单）

| 组 | 依据 | 例子 | 组数 |
|---|---|---|---|
| `unit_<race>` | `UnitData.race` ∈ human/orc/undead/nightelf/naga/demon/commoner/critters，且 `isbldg = 0` | `unit_human`（78） | 8 |
| `bldg_<race>` | 同上但 `isbldg = 1` | `bldg_orc`（18） | 5 |
| `creep_<class>` | `UnitUI.unitClass`，野怪里出现 ≥4 次的风格族 | `creep_bandit`（6） | 38 |
| `creep_misc_N` | 小族 / 无族的野怪，每 25 个一组 | `creep_misc_1` | 5 |
| `unit_other` / `bldg_other` | `race = other` | — | 2 |

合计 **55 个场景 / 836 个单位**（1 个 `LOSBlocker` 在表里就是「故意留白」，无模型属正常）。

`creep_*` 族的实测分布（前 8）：`draenei` 10、`revenant` 9、`arachnathid` / `furbolg` / `lobstrokk` / `ogre` / `tuskarr` 各 7、
`bandit` / `centaur` / `darktroll` / `foresttroll` / `icetroll` / `murgul` / `murloc` / `nerubian` / `razormane` / `giantspider` / `wolf` 各 6。

## 每个场景的结构

```
<group_key>            Node3D        meta: map_width/map_height/view_dist/view_target
├── Ground             MeshInstance3D 深灰底板（PlaneMesh）
├── <unitID>           实例（per-model .tscn，材质/动画/状态机都在源场景里）
└── label_<unitID>     Label3D        billboard，跑在马头上方
```

## 尺寸与取景

格距 380 单位（约 3 格），能放下最大的建筑而不重叠。`view_dist` 按内容算：
`max(宽度 × 0.7, 深度 × 1.15, 700)`，横向视野约 `dist × 1.64`、纵向约 `dist × 0.96`。

## 注意

- 模型源 `.tscn` 自带 `AnimationPlayer autoplay = Stand`，场景里单位会一直做待机动作。看静态姿态可临时把 autoplay 清空。
- **路径大小写**：`ModelLibrary.Find` 必须返回磁盘真实大小写。Windows 文件系统不区分大小写，
  早期版本按 slk 的小写路径返回，写进 `.tscn` 后 Godot 严格匹配就报
  `referenced non-existent resource at: res://assets/converted/units/creeps/...`（整组模型消失）。
