using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 任务处理管理器
/// </summary>
public class MonoManager :SingleMonoBase<MonoManager>
{
    private Action updataAction;//任务集合
    /// <summary>
    /// 添加任务
    /// </summary>
    /// <param name="task">事件</param>
    public void AddUpdateAction(Action task)
    {
        updataAction+= task;
    }

    public void RemoveUpdateAction(Action task)
    {
        updataAction -= task;
    }

   void Update()
    {
        updataAction?.Invoke();
    }
}
