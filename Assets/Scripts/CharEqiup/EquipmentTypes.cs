namespace CharacterEquipment
{
    /// <summary>
    /// Физические точки на персонаже, куда что-то крепится.
    /// Лево/право — отдельные точки, поэтому парная броня ставится на обе стороны.
    /// </summary>
    public enum AttachmentPointId
    {
        Head,
        Body,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg,
        LeftHand,
        RightHand
    }

    /// <summary>
    /// Категория предмета = слот в меню выбора.
    /// Arms и Legs в меню — один слот, но ставятся на обе конечности.
    /// Если порядок значений уже сохранён в ассетах, оставьте Helmet/Armor/Weapon первыми.
    /// </summary>
    public enum EquipmentCategory
    {
        Helmet,
        Armor,
        Weapon,
        Arms,
        Legs
    }

    /// <summary>
    /// Подкатегория оружия (вкладки в UI) + определяет, как предмет держат.
    /// Shield — левая рука, TwoHanded и Ranged — обе руки.
    /// </summary>
    public enum WeaponSubCategory
    {
        None,
        OneHanded,
        TwoHanded,
        Ranged,
        Shield
    }

    /// <summary>
    /// Логические слоты экипировки (то, что реально хранится на персонаже).
    /// </summary>
    public enum EquipmentSlot
    {
        Helmet,
        Body,
        Arms,
        Legs,
        RightHand,
        LeftHand
    }

    public enum PreferredHand
    {
        Right,
        Left
    }

    /// <summary>
    /// Поза руки/рук. Значение уходит в параметр аниматора как int,
    /// поэтому порядок не менять без пересборки аниматора.
    /// </summary>
    public enum HoldPose
    {
        None = 0,
        Sword = 1,
        Shield = 2,
        Staff = 3,
        Bow = 4
    }
}