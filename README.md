# Editor · 地图编辑器

地形笔刷、单位 / 可破坏物摆放、地图渲染。

工程内不含美术资源，运行时从 `extract.pck` 读取。

## 资源

启动时 `ExternalAssets.Mount()` 按顺序：

1. `res://extract` 存在 → 直接用
2. 否则读 exe 同级 `extract.pck` → `ProjectSettings.LoadResourcePack()`

| 场合 | 做法 |
|---|---|
| 直接跑 `godot --path Editor` | 模型为空，属正常 |
| 看完整画面 | 把 `extract.pck` 放到 exe 旁边 |

```
D:\WorldEditor\
├── Editor.exe
├── Editor.pck
└── extract.pck
```

## 命令

```bat
godot --path . --map="<地图>"
godot --path . --map="<地图>" --shot=out.png --no-brush --frames=120
godot --path . --scene=res://Demo/artlab/units/unit_human.tscn --shot=out.png --pitch=-8 --dist=320
godot --headless --path . -- "--map-report=<地图>"
godot --path . --map="<地图>" --unit-dump="<地图>" --doodad-dump="<地图>"
godot --headless --path . -- --art-lab-units=D:/WorldEditor/Editor/Demo/artlab/units
godot --headless --path . -- --art-lab-terrain=D:/WorldEditor/Editor/Demo/artlab/terrain
```

## 打包

```bat
extract.bat     资源 + extract.pck
export.bat      Editor.exe
```

## 目录

| 路径 | 内容 |
|---|---|
| `src/Core/Formats/W3x` | 地图解析（w3e / w3i / doo / wpm / w3u / w3d） |
| `src/Core/Formats/Slk` | 表格结构（读 `converted/Tables/*.json`） |
| `src/Core/Formats/Mpq` | 仅供 `War3Assets` 回退 |
| `src/Core/Map` | `MapDocument` |
| `src/Game/Assets` | 地形/悬崖/水面/单位/装饰物装配，资源定位 |
| `src/Game/Ui` | 视口、小地图、调色板、窗口 |
| `Demo/artlab` | 美术验证场景（生成物） |

## 注意

- `assets/shaders` 与 `Extract/assets/shaders` 是两份副本，改动需同步
- 解压逻辑不属于本工程
