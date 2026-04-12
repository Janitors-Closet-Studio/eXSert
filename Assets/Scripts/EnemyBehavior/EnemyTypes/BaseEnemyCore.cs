/*
 * Written by Will T
 * 
 * Added class for BaseEnemy to inherit from to make scripts outside of BaseEnemy easier to adjust and manipulate enemies
 */

using UnityEngine;

public abstract class BaseEnemyCore : MonoBehaviour, IHealthSystem
{
    public event System.Action<BaseEnemyCore> OnDeathStarted;
    public event System.Action<BaseEnemyCore> OnDeath;
    public event System.Action<BaseEnemyCore> OnSpawn;
    public event System.Action<BaseEnemyCore> OnReset;

    protected void InvokeOnDeathStarted() => OnDeathStarted?.Invoke(this);
    protected void InvokeOnDeath() => OnDeath?.Invoke(this);
    protected void InvokeOnSpawn() => OnSpawn?.Invoke(this);
    protected void InvokeOnReset() => OnReset?.Invoke(this);

    public abstract bool isAlive { get; }
    public abstract float currentHP { get; }
    public abstract float maxHP { get; }

    public abstract void Spawn();
    public abstract void ResetEnemy();
    public abstract void HealHP(float hp);
    public abstract void LoseHP(float damage);

    public virtual void ApplyHitStagger(float duration)
    {
    }
}
