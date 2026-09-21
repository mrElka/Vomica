using System;

namespace CharacterEquipment
{
    /// <summary>
    /// Характеристика в формате "название - прогресс бар"
    /// Прогресс бар состоит из значения текущего и максимального
    /// Например "Урон" 25/50, "Класс брони" 3/4
    /// </summary>
    /// 
    [Serializable]
    public class EquipmentStat
    {
        public string label;
        public float value;
        public float maxValue = 1f;

        /// <summary> Заполненность полоски от 0 до 1 </summary>
        public float NormalizedValue => maxValue > 0f ? UnityEngine.Mathf.Clamp01(value / maxValue) : 0f;
    }
}