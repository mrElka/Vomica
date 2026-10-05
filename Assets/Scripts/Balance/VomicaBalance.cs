using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Таблицы и расчёты из документа "Баланс оружия и снаряжения, версия 0.1".
/// Одна точка правды: и игрок, и противники берут показатели отсюда по классу.
/// </summary>
public static class VomicaBalance
{
    /// <summary>Активная фаза, когда оружие может нанести урон, с (раздел 3).</summary>
    public const float WeaponActivePhase = 0.10f;

    /// <summary>Сила обычного блока баклером (раздел 6).</summary>
    public const float BucklerBlockPower = 12f;

    /// <summary>Сила парирования баклером (раздел 6).</summary>
    public const float BucklerParryPower = 30f;

    /// <summary>Окно парирования после поднятия щита, с (раздел 6).</summary>
    public const float BucklerParryWindow = 0.20f;

    // ================= ОРУЖИЕ (раздел 3) =================

    private static readonly Dictionary<WeaponClass, WeaponClassStats> WeaponTable =
        new Dictionary<WeaponClass, WeaponClassStats>
    {
        //                                       название            урон и тип                         дальн. цикл  замах стам. руки
        { WeaponClass.Unarmed,        new WeaponClassStats("Без оружия",      new DamagePacket(0f, 0f, 0f),   1.2f, 0.60f, 0.20f,  5f, 1) },
        { WeaponClass.Club,           new WeaponClassStats("Дубина",          new DamagePacket(0f, 0f, 16f),  1.4f, 0.85f, 0.25f, 10f, 1) },
        { WeaponClass.Dagger,         new WeaponClassStats("Кинжал",          new DamagePacket(0f, 12f, 0f),  1.0f, 0.50f, 0.15f,  7f, 1) },
        { WeaponClass.Sword,          new WeaponClassStats("Меч",             new DamagePacket(22f, 0f, 0f),  1.8f, 0.90f, 0.25f, 12f, 1) },
        { WeaponClass.TwoHandedSword, new WeaponClassStats("Двуручный меч",   new DamagePacket(36f, 0f, 0f),  2.3f, 1.40f, 0.45f, 20f, 2) },
        { WeaponClass.SpikedMace,     new WeaponClassStats("Булава с шипами", new DamagePacket(0f, 6f, 20f),  1.5f, 1.15f, 0.35f, 16f, 1) },
        { WeaponClass.Hammer,         new WeaponClassStats("Молот",           new DamagePacket(0f, 0f, 40f),  1.7f, 1.80f, 0.65f, 26f, 2) },
        { WeaponClass.Spear,          new WeaponClassStats("Копьё",           new DamagePacket(0f, 21f, 0f),  2.8f, 1.05f, 0.30f, 14f, 2) },
        { WeaponClass.Halberd,        new WeaponClassStats("Алебарда",        new DamagePacket(32f, 0f, 0f),  2.6f, 1.40f, 0.45f, 20f, 2) },
        { WeaponClass.Buckler,        new WeaponClassStats("Баклер: толчок",  new DamagePacket(0f, 0f, 8f),   0.8f, 0.80f, 0.20f, 10f, 0) },
    };

    /// <summary>Показатели класса оружия. Неизвестный класс отдаёт "Без оружия".</summary>
    public static WeaponClassStats GetWeapon(WeaponClass weaponClass)
    {
        return WeaponTable.TryGetValue(weaponClass, out WeaponClassStats stats)
            ? stats
            : WeaponTable[WeaponClass.Unarmed];
    }

    // ================= МАТЕРИАЛЫ БРОНИ (раздел 4) =================

    /// <summary>
    /// Коэффициент материала против типа урона. Умножает защиту A, а не сам урон:
    /// при 1.5 защита растёт в полтора раза, а не урон падает на 50%.
    /// </summary>
    public static float GetProfileCoefficient(ArmorProfile profile, DamageType type)
    {
        switch (profile)
        {
            case ArmorProfile.Quilted:
                return type == DamageType.Slashing ? 0.80f
                     : type == DamageType.Piercing ? 0.60f
                     : 1.50f;

            case ArmorProfile.Leather:
                return type == DamageType.Slashing ? 1.00f
                     : type == DamageType.Piercing ? 0.80f
                     : 0.80f;

            case ArmorProfile.Mail:
                return type == DamageType.Slashing ? 1.50f
                     : type == DamageType.Piercing ? 1.00f
                     : 0.60f;

            case ArmorProfile.Plate:
                return type == DamageType.Slashing ? 1.30f
                     : type == DamageType.Piercing ? 1.50f
                     : 0.60f;

            case ArmorProfile.ReinforcedPadded:
                return type == DamageType.Slashing ? 1.10f
                     : type == DamageType.Piercing ? 1.10f
                     : 1.25f;

            default:
                return 0f;
        }
    }

    /// <summary>
    /// Общий коэффициент для смешанного удара: доли типов урона задают вес.
    /// Для булавы k = (20 × k_дроб + 6 × k_кол) / 26 (раздел 2).
    /// </summary>
    public static float GetBlendedCoefficient(DamagePacket damage, ArmorProfile profile)
    {
        float total = damage.Total;
        if (total <= 0f) return 0f;

        float weighted =
            damage.Slashing * GetProfileCoefficient(profile, DamageType.Slashing) +
            damage.Piercing * GetProfileCoefficient(profile, DamageType.Piercing) +
            damage.Blunt * GetProfileCoefficient(profile, DamageType.Blunt);

        return weighted / total;
    }

    /// <summary>Эффективная защита зоны: Aэфф = A × коэффициент материала.</summary>
    public static float GetEffectiveArmor(DamagePacket damage, float armorA, ArmorProfile profile)
    {
        if (armorA <= 0f) return 0f;
        return armorA * GetBlendedCoefficient(damage, profile);
    }

    // ================= РАСЧЁТ УРОНА (раздел 2) =================

    /// <summary>
    /// Потеря HP от одного попадания. Формула в духе Valheim: при низкой защите
    /// она вычитается из урона, при высокой включается вторая ветвь.
    /// Броня применяется к смешанному удару один раз.
    /// </summary>
    public static float ComputeHpLoss(DamagePacket damage, float armorA, ArmorProfile profile)
    {
        float total = damage.Total;
        if (total <= 0f) return 0f;

        return ComputeHpLoss(total, GetEffectiveArmor(damage, armorA, profile));
    }

    /// <summary>
    /// Та же формула для уже посчитанной эффективной защиты. Используется и для
    /// блока баклером, где вместо Aэфф подставляется сила блока B (раздел 6).
    /// </summary>
    public static float ComputeHpLoss(float damage, float effectiveArmor)
    {
        if (damage <= 0f) return 0f;
        if (effectiveArmor <= 0f) return damage;
        if (effectiveArmor < damage / 2f) return damage - effectiveArmor;

        return damage * damage / (4f * effectiveArmor);
    }

    /// <summary>Стоимость обычного блока: 12 + 0.4 × D, где D — полный урон до блока.</summary>
    public static float GetBlockStaminaCost(float incomingDamage) => 12f + 0.4f * incomingDamage;

    /// <summary>Стоимость парирования: 10 + 0.2 × D.</summary>
    public static float GetParryStaminaCost(float incomingDamage) => 10f + 0.2f * incomingDamage;

    // ================= НАБОР БРОНИ (раздел 5) =================

    /// <summary>
    /// Четырнадцать предметов: пять шлемов и девять вариантов защиты корпуса, рук и ног.
    /// Свободный слот — это A = 0 без штрафов.
    /// </summary>
    public static readonly ArmorItemPreset[] ArmorPresets =
    {
        //                  предмет                        название                    зона                     класс                      A   профиль                         движ.  атака
        new ArmorItemPreset(ArmorPiece.HeadBucketT0,     "BUCKET, T0",            ArmorZoneGroup.Head,  EquipWeightClass.Light,   4f, ArmorProfile.Plate,            0.01f, 0.00f),
        new ArmorItemPreset(ArmorPiece.HeadVelesT1,      "VELES, T1",             ArmorZoneGroup.Head,  EquipWeightClass.Light,   7f, ArmorProfile.Leather,          0.00f, 0.00f),
        new ArmorItemPreset(ArmorPiece.HeadGladiatrixT2, "GLADIATRIX, T2",        ArmorZoneGroup.Head,  EquipWeightClass.Medium, 10f, ArmorProfile.Plate,            0.01f, 0.00f),
        new ArmorItemPreset(ArmorPiece.HeadSecutorT3,    "SECUTOR, T3",           ArmorZoneGroup.Head,  EquipWeightClass.Heavy,  13f, ArmorProfile.Plate,            0.02f, 0.01f),
        new ArmorItemPreset(ArmorPiece.HeadProvocatorT4, "PROVOCATOR, T4",        ArmorZoneGroup.Head,  EquipWeightClass.Heavy,  16f, ArmorProfile.ReinforcedPadded, 0.03f, 0.01f),

        new ArmorItemPreset(ArmorPiece.TorsoQuilted,     "Стёганый нагрудник",    ArmorZoneGroup.Torso, EquipWeightClass.Light,   8f, ArmorProfile.Quilted,          0.02f, 0.00f),
        new ArmorItemPreset(ArmorPiece.TorsoMail,        "Кольчужная рубаха",     ArmorZoneGroup.Torso, EquipWeightClass.Medium, 12f, ArmorProfile.Mail,             0.04f, 0.01f),
        new ArmorItemPreset(ArmorPiece.TorsoPlate,       "Пластинчатый нагрудник",ArmorZoneGroup.Torso, EquipWeightClass.Heavy,  14f, ArmorProfile.Plate,            0.06f, 0.02f),

        new ArmorItemPreset(ArmorPiece.ArmQuilted,       "Стёганая маника",       ArmorZoneGroup.Arm,   EquipWeightClass.Light,   6f, ArmorProfile.Quilted,          0.00f, 0.01f),
        new ArmorItemPreset(ArmorPiece.ArmMail,          "Кольчужная маника",     ArmorZoneGroup.Arm,   EquipWeightClass.Medium, 10f, ArmorProfile.Mail,             0.01f, 0.02f),
        new ArmorItemPreset(ArmorPiece.ArmPlate,         "Пластинчатая маника",   ArmorZoneGroup.Arm,   EquipWeightClass.Heavy,  12f, ArmorProfile.Plate,            0.01f, 0.03f),

        new ArmorItemPreset(ArmorPiece.LegQuilted,       "Стёганая поножа",       ArmorZoneGroup.Leg,   EquipWeightClass.Light,   6f, ArmorProfile.Quilted,          0.01f, 0.00f),
        new ArmorItemPreset(ArmorPiece.LegMail,          "Кольчужная поножа",     ArmorZoneGroup.Leg,   EquipWeightClass.Medium, 10f, ArmorProfile.Mail,             0.02f, 0.00f),
        new ArmorItemPreset(ArmorPiece.LegPlate,         "Пластинчатая поножа",   ArmorZoneGroup.Leg,   EquipWeightClass.Heavy,  12f, ArmorProfile.Plate,            0.03f, 0.00f),
    };

    /// <summary>Строка таблицы по предмету набора. false — предмет не задан (None).</summary>
    public static bool TryGetArmorPreset(ArmorPiece piece, out ArmorItemPreset preset)
    {
        if (piece != ArmorPiece.None)
        {
            foreach (ArmorItemPreset item in ArmorPresets)
            {
                if (item.Piece != piece) continue;

                preset = item;
                return true;
            }

            Debug.LogError($"[Balance] В таблице брони нет строки для {piece}");
        }

        preset = default;
        return false;
    }

    /// <summary>Предмет набора по названию. Возвращает false, если такого нет.</summary>
    public static bool TryGetArmorPreset(string displayName, out ArmorItemPreset preset)
    {
        foreach (ArmorItemPreset item in ArmorPresets)
        {
            if (item.DisplayName == displayName)
            {
                preset = item;
                return true;
            }
        }

        preset = default;
        return false;
    }

    // ================= ШТРАФЫ КОМПЛЕКТА (раздел 5) =================

    /// <summary>Скорость движения = базовая × (1 − сумма штрафов движения).</summary>
    public static float ApplyMovementPenalty(float baseSpeed, float penaltySum) =>
        baseSpeed * Mathf.Clamp01(1f - penaltySum);

    /// <summary>Цикл атаки = базовый цикл / (1 − сумма штрафов атак).</summary>
    public static float ApplyAttackPenalty(float baseCycle, float penaltySum)
    {
        float scale = 1f - penaltySum;
        return scale <= 0.05f ? baseCycle / 0.05f : baseCycle / scale;
    }
}
