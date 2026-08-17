using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色状态基类
/// </summary>



public abstract class StateBase //抽象类，抽象方法
{

    /// <summary>
    /// 初始化
    /// </summary>
    public abstract void Init(IStateMachineOwner owner);

    /// <summary>
    /// 进入状态
    /// </summary>
    public abstract void Enter();


    /// <summary>
    /// 退出状态
    /// </summary>
    public abstract void Exit();


    /// <summary>
    /// 销毁
    /// </summary>
    public abstract void Destory();

    public abstract void Update();




}
