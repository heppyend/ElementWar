using System;
using System.Collections.Generic;

namespace ElementWar.Combat
{
    public struct PlayerInputSnapshot
    {
        public float moveX;
        public float moveY;
        public bool isSprint;
        public bool isAiming;
    }

    public enum CombatIntentType { Fire, Reload }

    public struct CombatIntent
    {
        public long sequence;
        public CombatIntentType type;
        public CombatIntent(long sequence, CombatIntentType type) { this.sequence = sequence; this.type = type; }
    }

    /// <summary>一次性意图去重与消费，避免同一输入边沿被重复处理。</summary>
    public sealed class CombatIntentBuffer
    {
        private readonly Queue<CombatIntent> pending = new Queue<CombatIntent>();
        private readonly HashSet<long> seenSequences = new HashSet<long>();
        public bool TryEnqueue(CombatIntent intent)
        {
            if (intent.sequence <= 0 || !seenSequences.Add(intent.sequence)) return false;
            pending.Enqueue(intent);
            return true;
        }
        public bool TryConsume(out CombatIntent intent)
        {
            if (pending.Count == 0) { intent = default; return false; }
            intent = pending.Dequeue();
            return true;
        }
        public void Clear() { pending.Clear(); seenSequences.Clear(); }
    }

    public sealed class WeaponDefinition
    {
        public readonly int magazineCapacity;
        public readonly int damage;
        public readonly float fireIntervalSeconds;
        public readonly float reloadDurationSeconds;
        public WeaponDefinition(int magazineCapacity, int damage, float fireIntervalSeconds, float reloadDurationSeconds)
        {
            if (magazineCapacity <= 0 || damage <= 0 || fireIntervalSeconds < 0f || reloadDurationSeconds < 0f)
                throw new ArgumentOutOfRangeException();
            this.magazineCapacity = magazineCapacity;
            this.damage = damage;
            this.fireIntervalSeconds = fireIntervalSeconds;
            this.reloadDurationSeconds = reloadDurationSeconds;
        }
    }

    public enum CombatResultKind { FireAccepted, FireRejected, ReloadStarted, ReloadCompleted, ReloadRejected, DamageApplied }
    public enum CombatRejectReason { None, FireRateLimited, EmptyMagazine, Reloading, NoReserveAmmo, MagazineFull, InvalidRequest }

    /// <summary>规则层给表现/UI 的稳定结论。</summary>
    public struct CombatResult
    {
        public CombatResultKind kind;
        public CombatRejectReason rejectReason;
        public long requestId;
        public int magazineAmmo;
        public int reserveAmmo;
        public int damage;
        public int targetHealth;
        public bool targetDied;
    }

    public struct CombatRequest
    {
        public long requestId;
        public CombatIntentType intentType;
        public float requestedAtSeconds;
        public CombatRequest(long requestId, CombatIntentType intentType, float requestedAtSeconds)
        { this.requestId = requestId; this.intentType = intentType; this.requestedAtSeconds = requestedAtSeconds; }
    }

    /// <summary>不依赖 Unity 时间、物理或表现对象的武器运行时。</summary>
    public sealed class WeaponRuntime
    {
        private readonly WeaponDefinition definition;
        private float nextFireAllowedAt;
        private float reloadCompletesAt;
        private long activeReloadRequestId;
        public int MagazineAmmo { get; private set; }
        public int ReserveAmmo { get; private set; }
        public bool IsReloading { get; private set; }

        public WeaponRuntime(WeaponDefinition definition, int initialMagazineAmmo, int initialReserveAmmo)
        {
            this.definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (initialMagazineAmmo < 0 || initialMagazineAmmo > definition.magazineCapacity || initialReserveAmmo < 0)
                throw new ArgumentOutOfRangeException();
            MagazineAmmo = initialMagazineAmmo;
            ReserveAmmo = initialReserveAmmo;
        }

        public CombatResult Resolve(CombatRequest request)
        {
            if (request.requestId <= 0 || request.requestedAtSeconds < 0f) return Rejected(request, CombatRejectReason.InvalidRequest);
            return request.intentType == CombatIntentType.Fire ? ResolveFire(request) : ResolveReload(request);
        }

        public bool TryCompleteReload(float nowSeconds, out CombatResult result)
        {
            if (!IsReloading || nowSeconds < reloadCompletesAt) { result = default; return false; }
            int moved = Math.Min(definition.magazineCapacity - MagazineAmmo, ReserveAmmo);
            MagazineAmmo += moved;
            ReserveAmmo -= moved;
            IsReloading = false;
            result = new CombatResult { kind = CombatResultKind.ReloadCompleted, requestId = activeReloadRequestId, magazineAmmo = MagazineAmmo, reserveAmmo = ReserveAmmo };
            activeReloadRequestId = 0;
            return true;
        }

        public void ApplyAuthoritativeState(int magazineAmmo, int reserveAmmo, bool isReloading)
        {
            MagazineAmmo = Math.Max(0, Math.Min(magazineAmmo, definition.magazineCapacity));
            ReserveAmmo = Math.Max(0, reserveAmmo);
            IsReloading = isReloading;
            reloadCompletesAt = isReloading ? float.PositiveInfinity : 0f;
            if (!isReloading) activeReloadRequestId = 0;
        }

        private CombatResult ResolveFire(CombatRequest request)
        {
            if (IsReloading) return Rejected(request, CombatRejectReason.Reloading);
            if (MagazineAmmo <= 0) return Rejected(request, CombatRejectReason.EmptyMagazine);
            if (request.requestedAtSeconds < nextFireAllowedAt) return Rejected(request, CombatRejectReason.FireRateLimited);
            MagazineAmmo--;
            nextFireAllowedAt = request.requestedAtSeconds + definition.fireIntervalSeconds;
            return new CombatResult { kind = CombatResultKind.FireAccepted, requestId = request.requestId, magazineAmmo = MagazineAmmo, reserveAmmo = ReserveAmmo, damage = definition.damage };
        }

        private CombatResult ResolveReload(CombatRequest request)
        {
            if (IsReloading) return Rejected(request, CombatRejectReason.Reloading);
            if (MagazineAmmo >= definition.magazineCapacity) return Rejected(request, CombatRejectReason.MagazineFull);
            if (ReserveAmmo <= 0) return Rejected(request, CombatRejectReason.NoReserveAmmo);
            IsReloading = true;
            reloadCompletesAt = request.requestedAtSeconds + definition.reloadDurationSeconds;
            activeReloadRequestId = request.requestId;
            return new CombatResult { kind = CombatResultKind.ReloadStarted, requestId = request.requestId, magazineAmmo = MagazineAmmo, reserveAmmo = ReserveAmmo };
        }

        private CombatResult Rejected(CombatRequest request, CombatRejectReason reason)
        {
            return new CombatResult { kind = request.intentType == CombatIntentType.Fire ? CombatResultKind.FireRejected : CombatResultKind.ReloadRejected, rejectReason = reason, requestId = request.requestId, magazineAmmo = MagazineAmmo, reserveAmmo = ReserveAmmo };
        }
    }

    public static class CombatDamageResolver
    {
        public static CombatResult ApplyDamage(long requestId, int currentHealth, int damage)
        {
            if (currentHealth < 0 || damage <= 0) throw new ArgumentOutOfRangeException();
            int targetHealth = Math.Max(0, currentHealth - damage);
            return new CombatResult { kind = CombatResultKind.DamageApplied, requestId = requestId, damage = damage, targetHealth = targetHealth, targetDied = targetHealth == 0 };
        }
    }
}
