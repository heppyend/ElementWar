using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public interface IStateMachineOwner { }//状态机宿主机
/// <summary>
/// 角色状态机
/// </summary>
public class StateMachine {

    private StateBase currentState;//当前状态
    private IStateMachineOwner owner;//状态宿主

    private Dictionary<Type, StateBase> stateDic = new Dictionary<Type, StateBase>();//状态字典

    public StateMachine(IStateMachineOwner owner)
    {
        this.owner = owner;
    }
    /// <summary>
    /// 进入动画状态
    /// </summary>
    /// <typeparam name="T">状态类</typeparam>
    public void EnterState<T>() where T : StateBase, new() {
        //防止重复进入同一个动画状态
        if (currentState != null && currentState.GetType() == typeof(T)) return;

        if (currentState != null)
            currentState.Exit();

        currentState = LoadState<T>();
        currentState.Enter();
    }

    /// <summary>
    /// 尝试从字典中取出状态
    /// </summary>
    /// <typeparam name="T">状态类</typeparam>
    /// <returns>状态实例</returns>
    private StateBase LoadState<T>() where T : StateBase, new() {
        Type stateType=typeof(T);//获取状态类型
        //如果状态字典里没有该状态
        if(!stateDic.TryGetValue(stateType,out StateBase state))
        {
            state=new T();
            state.Init(owner);
            stateDic.Add(stateType, state);//将新创建的状态记录到字典中
        }
        return state;
    }
    /// <summary>
    /// 停止状态机 
    /// </summary>
    public void Stop()
    {
        if (currentState != null)
            currentState.Exit();
        foreach (var state in stateDic.Values)
            state.Destory();
        stateDic.Clear();
    }
}
