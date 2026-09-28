using System;
using System.Text;
using UnityEngine;

// Типы для системы классов из документа "Баланс оружия и снаряжения, версия 0.1".
// Числа и формулы живут в VomicaBalance.

/// <summary>Три физических типа урона (раздел 1).</summary>
public enum DamageType
{
    /// <summary>Рубящий.</summary>
    Slashing,
    /// <summary>Колющий.</summary>
    Piercing,
    /// <summary>Дробящий.</summary>
    Blunt
}

/// <summary>
/// Класс оружия. Задаёт урон и его тип, дальность, цикл, замах,
/// расход выносливости и количество рук (раздел 3).
/// </summary>
public enum WeaponClass
{
    /// <summary>Без оружия: показатели берутся из полей скрипта, а не из таблицы.</summary>
    Unarmed,
    /// <summary>Дубина.</summary>
    Club,
    /// <summary>Кинжал.</summary>
    Dagger,
    /// <summary>Меч.</summary>
    Sword,
    /// <summary>Двуручный меч.</summary>
    TwoHandedSword,
    /// <summary>Булава с шипами: смешанный удар.</summary>
    SpikedMace,
    /// <summary>Молот.</summary>
    Hammer,
    /// <summary>Копьё.</summary>
    Spear,
    /// <summary>Алебарда.</summary>
    Halberd,
    /// <summary>Баклер: толчок левой рукой.</summary>
    Buckler
}

/// <summary>
/// Профиль материала брони. Коэффициенты умножают защиту A, а не входящий урон (раздел 4).
/// </summary>
public enum ArmorProfile
{
    /// <summary>Открытая зона: защиты нет.</summary>
    None,
    /// <summary>Стёганый: слои льна, набивка, кожаные крепления.</summary>
    Quilted,
    /// <summary>Кожаный: плотная кожа с мягкой подкладкой.</summary>
    Leather,
    /// <summary>Кольчужный: железные кольца с тонкой подкладкой.</summary>
    Mail,
    /// <summary>Пластинчатый: железные или стальные пластины.</summary>
    Plate,
    /// <summary>Усиленный с набивкой: металлическая оболочка с толстой подкладкой.</summary>
    ReinforcedPadded
}

/// <summary>Категория веса предмета. Сама по себе не увеличивает защиту A (раздел 5).</summary>
public enum EquipWeightClass
{
    /// <summary>Лёгкий.</summary>
    Light,
    /// <summary>Средний.</summary>
    Medium,
    /// <summary>Тяжёлый.</summary>
    Heavy
}

/// <summary>Зона тела. Броня действует только на поражённую часть (раздел 1).</summary>
public enum BodyZone
{
    Head,
    Torso,
    LeftArm,
    RightArm,
    LeftLeg,
    RightLeg
}

/// <summary>Зона, под которую сделан предмет. Левая и правая стороны равны по характеристикам.</summary>
public enum ArmorZoneGroup
{
    Head,
    Torso,
    Arm,
    Leg
}

/// <summary>
/// Урон одного попадания, разложенный по типам. Смешанный удар булавы
/// (20 дробящего + 6 колющего) считается одним попаданием (раздел 2).
/// </summary>
[Serializable]
public struct DamagePacket
{
    [Tooltip("Рубящий")] public float Slashing;
    [Tooltip("Колющий")] public float Piercing;
    [Tooltip("Дробящий")] public float Blunt;

    public DamagePacket(float slashing, float piercing, float blunt)
    {
        Slashing = slashing;
        Piercing = piercing;
        Blunt = blunt;
    }

    /// <summary>Удар одного типа.</summary>
    public static DamagePacket Of(DamageType type, float amount)
    {
        switch (type)
        {
            case DamageType.Slashing: return new DamagePacket(amount, 0f, 0f);
            case DamageType.Piercing: return new DamagePacket(0f, amount, 0f);
            default: return new DamagePacket(0f, 0f, amount);
        }
    }

    public float Total => Slashing + Piercing + Blunt;

    public bool IsEmpty => Total <= 0f;

    public float Get(DamageType type)
    {
        switch (type)
        {
            case DamageType.Slashing: return Slashing;
            case DamageType.Piercing: return Piercing;
            default: return Blunt;
        }
    }

    /// <summary>Усиление атаки применяется до брони (раздел 2).</summary>
    public DamagePacket Scaled(float multiplier) =>
        new DamagePacket(Slashing * multiplier, Piercing * multiplier, Blunt * multiplier);

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        if (Blunt > 0f) sb.Append($"{Blunt:0.##} дроб.");
        if (Piercing > 0f) sb.Append(sb.Length > 0 ? $" + {Piercing:0.##} кол." : $"{Piercing:0.##} кол.");
        if (Slashing > 0f) sb.Append(sb.Length > 0 ? $" + {Slashing:0.##} руб." : $"{Slashing:0.##} руб.");

        return sb.Length > 0 ? sb.ToString() : "0";
    }
}

/// <summary>Табличные показатели одного класса оружия (раздел 3).</summary>
public readonly struct WeaponClassStats
{
    public readonly string DisplayName;
    public readonly DamagePacket Damage;

    /// <summary>Максимальная дальность удара от центра атакующего, м.</summary>
    public readonly float Range;

    /// <summary>Полный цикл: замах, момент нанесения урона и восстановление, с.</summary>
    public readonly float Cycle;

    /// <summary>Замах до момента нанесения урона, с.</summary>
    public readonly float Windup;

    /// <summary>Расход выносливости за один удар.</summary>
    public readonly float StaminaCost;

    /// <summary>1 — одноручное, 2 — двуручное, 0 — предмет левой руки.</summary>
    public readonly int Hands;

    public WeaponClassStats(
        string displayName,
        DamagePacket damage,
        float range,
        float cycle,
        float windup,
        float staminaCost,
        int hands)
    {
        DisplayName = displayName;
        Damage = damage;
        Range = range;
        Cycle = cycle;
        Windup = windup;
        StaminaCost = staminaCost;
        Hands = hands;
    }

    public float AttacksPerSecond => Cycle > 0f ? 1f / Cycle : 0f;

    /// <summary>Двуручное оружие несовместимо с баклером (раздел 3).</summary>
    public bool IsTwoHanded => Hands >= 2;
}

/// <summary>Предмет из небольшого набора брони (раздел 5).</summary>
public readonly struct ArmorItemPreset
{
    public readonly string DisplayName;
    public readonly ArmorZoneGroup Zone;
    public readonly EquipWeightClass WeightClass;

    /// <summary>Базовая защита A до коэффициента материала.</summary>
    public readonly float ArmorA;

    public readonly ArmorProfile Profile;

    /// <summary>Штраф движения за один надетый предмет, доля: 0.02 = −2%.</summary>
    public readonly float MovementPenalty;

    /// <summary>Штраф частоты атак за один надетый предмет, доля.</summary>
    public readonly float AttackPenalty;

    public ArmorItemPreset(
        string displayName,
        ArmorZoneGroup zone,
        EquipWeightClass weightClass,
        float armorA,
        ArmorProfile profile,
        float movementPenalty,
        float attackPenalty)
    {
        DisplayName = displayName;
        Zone = zone;
        WeightClass = weightClass;
        ArmorA = armorA;
        Profile = profile;
        MovementPenalty = movementPenalty;
        AttackPenalty = attackPenalty;
    }
}
