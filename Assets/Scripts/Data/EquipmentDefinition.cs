using UnityEngine;

// One equippable item — weapon, armor, or accessory (ring/amulet/relic).
// Stat bonuses ride a CharacterStats block and are folded into
// CombatantData.GetTotalStats alongside job contributions, so equipment
// stacks with everything else. Append-only on the slot enum.
// Append-only: existing entries never move.
public enum EquipSlot { Weapon, Armor, Accessory, Helmet, Gloves, Boots }

[CreateAssetMenu(fileName = "Item_New", menuName = "InfernosCurse/Equipment")]
public class EquipmentDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemName;
    [TextArea(2, 3)]
    public string description;
    public Sprite icon;
    public EquipSlot slot;

    [Header("Stat Bonuses (added to total stats while equipped)")]
    public CharacterStats bonuses = new CharacterStats();

    [Header("Weapon-only")]
    [Tooltip("Element carried by basic attacks with this weapon (None = physical).")]
    public DamageType damageType = DamageType.Physical;
    [Tooltip("Added to basic attack power.")]
    public int attackPowerBonus = 0;

    [Header("Economy")]
    public int valueFlorins = 10;
}
