namespace FPS
{
    /// <summary>
    /// 瞄准模式抽象基类（腰射 / 肩射 / 开镜）。
    /// 每种模式负责：相机切换、FOV、武器姿态。
    /// 新增瞄准模式：继承本类并实现 Enter/Update/Exit，再到 FPSAimState.ResolveMode 里分配即可。
    /// </summary>
    public abstract class FPSAimModeBase
    {
        protected FPSModel model;
        protected FPSController controller;

        /// <summary>绑定宿主（进入瞄准状态时由 FPSAimState 调用）</summary>
        public void Init(FPSModel model, FPSController controller)
        {
            this.model = model;
            this.controller = controller;
        }

        public abstract void Enter();
        public abstract void Update();
        public abstract void Exit();
    }
}
