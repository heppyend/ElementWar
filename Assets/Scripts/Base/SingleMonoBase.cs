using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 单例模式限定器
/// </summary>
/// <typeparam name="T">子类</typeparam>
public class SingleMonoBase<T> : MonoBehaviour where T : SingleMonoBase<T>
{
    public static T INSTANCE;

    protected virtual void Awake()
    {
        if(INSTANCE != null)
        {
            Debug.LogError(name + "不符合单例模式");
        }
        INSTANCE = (T)this;
    }

    protected virtual void OnDestroy()
    {
        INSTANCE = null;
    }
}
