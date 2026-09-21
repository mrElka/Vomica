namespace CharacterEquipment
{
    /// <summary>
    /// Привязка к персонажу, по поинтам, куда будет крепиться оружие
    /// Используется AttachmentPoint / CharacterEquipment.
    /// 
    /// Я не знаю что потом делать с двуручными
    /// </summary>
    public enum AttachmentSlotType
    {
        Helmet,
        Armor,
        Weapon,
        Shield
    }

    /// <summary>
    /// Главная категория (Шлема, армор, оружие)
    /// </summary>
    public enum EquipmentCategory
    {
        Helmet,
        Armor,
        Weapon
    }

    /// <summary>
    /// Подкатегория для оружия, используется только когда категория оружия активна, вот
    /// Щиты крепятся к своему слоту во второй руке
    /// 
    /// Все ещё хз че делать с двуручкой :)
    /// </summary>
    public enum WeaponSubCategory
    {
        None,       // не применимо / точка крепления принимает любую категорию
        OneHanded,
        TwoHanded,
        Ranged,
        Shield
    }
}