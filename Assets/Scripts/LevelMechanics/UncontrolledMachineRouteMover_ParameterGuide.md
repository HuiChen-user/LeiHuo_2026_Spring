# 失控机器参数说明

把 `UncontrolledMachineRouteMover` 挂到机器物体上，并确保机器或子物体上有 Collider，且该层级能被 `TemperatureFieldController` 的 `Affectable Layers` 检测到。选中机器后，Scene 视图会显示路线点，可直接拖拽编辑。

## Route

- `Route Space`：路线点的参考坐标系。为空时路线点按世界坐标保存；指定父物体后，路线会跟随该父物体整体移动。不要指定机器自身，否则路线会跟着机器一起移动。
- `Route Points`：机器依次经过的路径点。至少需要 2 个点才会移动。
- `Route Mode`：到达路线末端后的行为。`Loop` 循环回起点，`PingPong` 来回折返，`StopAtEnd` 到末端后停止。
- `Start Point Index`：运行开始时使用的起点序号。
- `Arrive Distance`：距离目标点小于该值时，认为机器已经到达并切换到下一个点。
- `Snap To Start On Play`：进入 Play 时是否把机器位置吸附到起点，便于测试路线。

## Movement

- `Move Speed`：机器沿路线移动的速度，单位是 Unity 单位/秒。失控感主要通过这个值调节。
- `Rotate To Move Direction`：是否让机器朝向移动方向。
- `Turn Speed`：转向速度，单位是度/秒。设为 0 时会瞬间朝向移动方向。
- `Use Rigidbody When Available`：机器上有 Rigidbody 时，使用 `MovePosition` / `MoveRotation` 移动，和物理系统配合更稳定。

## Temperature Stop

- `Stop Duration After Leaving Field`：离开角色温度场后继续保持停止的时间。
- `Reset Hold Timer While Staying In Field`：停在温度场内时是否持续刷新离场后的停止计时。通常保持开启，表示只要仍在场内，离场后才开始完整倒计时。

## Runtime

- `Move On Start`：开始运行时是否自动移动。关闭后可由其他脚本调用 `StartMoving()`。
- `Log State Changes`：是否在进入温度场、离开温度场、恢复移动时打印日志。

## Debug Preview

- `Show Route Gizmos`：是否在 Scene 视图显示路线。
- `Route Color`：正常路线颜色。
- `Stopped Color`：被温度场停止时的预览颜色。
- `Route Point Gizmo Radius`：路线点预览球大小。

## Scene 视图快捷编辑

- 拖拽 `P0`、`P1` 等位置手柄可以移动路径点。
- 按住 `Ctrl` 后点击线段中点的小圆，可以在该线段后插入路径点。
- 按住 `Alt` 后点击路径点球，可以删除该点；至少保留 2 个点。
