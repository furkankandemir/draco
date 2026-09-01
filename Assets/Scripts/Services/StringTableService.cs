using System.Collections.Generic;

namespace EntropyOnline.Services
{
    /// <summary>
    /// Open-KO: Texts_us.tbl — Oyun içi metin tablosu.
    /// text_resources.h enum değerlerinden port edilmiş sistem string'leri +
    /// Quest .evt dosyalarında kullanılan NPC diyalog metinleri.
    /// 
    /// Kullanım: StringTableService.Get(stringId)
    /// </summary>
    public static class StringTableService
    {
        private static readonly Dictionary<int, string> _table = new()
        {
            { 0, "" },
            { -1, "..." },
            { 1001, "Failed designating re-spawn point." }, // IDS_BIND_POINT_FAILED
            { 1002, "The re-spawn point is too far away." }, // IDS_BIND_POINT_REQUEST_FAIL
            { 1003, "Successfully designated a re-spawn point." }, // IDS_BIND_POINT_SUCCESS
            { 1004, "You've selected the wrong Warp Gate." }, // IDS_WARP_WRONG_GATE
            { 1101, "Failed to connect for a private chat" }, // IDS_CHAT_SELECT_TARGET_FAIL
            { 1102, "Successfully connected for a private chat" }, // IDS_CHAT_SELECT_TARGET_SUCCESS
            { 1201, "Level : %d \nSpecialty : %s \nID : %s" }, // IDS_CHR_SELECT_FMT_INFO
            { 1202, "Left click and\nyou can create a\nnew character." }, // IDS_CHR_SELECT_HINT
            { 1301, "How are you? We need people like you to work hard for our victory!" }, // IDS_CLASS_CHANGE_ALREADY
            { 1302, "I think you need more training" }, // IDS_CLASS_CHANGE_NOT_YET
            { 1303, "You can now obtain a greater strength. From now, you shall be called.." }, // IDS_CLASS_CHANGE_SUCCESS
            { 1304, "Warrior" }, // IDS_CLASS_KINDOF_WARRIOR
            { 1305, "Rogue" }, // IDS_CLASS_KINDOF_ROGUE
            { 1306, "Magician" }, // IDS_CLASS_KINDOF_WIZARD
            { 1307, "Priest" }, // IDS_CLASS_KINDOF_PRIEST
            { 1308, "Offensive Warrior" }, // IDS_CLASS_KINDOF_ATTACK_WARRIOR
            { 1309, "Defensive Warrior" }, // IDS_CLASS_KINDOF_DEFEND_WARRIOR
            { 1310, "Archer" }, // IDS_CLASS_KINDOF_ARCHER
            { 1311, "Assassin" }, // IDS_CLASS_KINDOF_ASSASSIN
            { 1312, "Offensive Magician" }, // IDS_CLASS_KINDOF_ATTACK_WIZARD
            { 1313, "Pet Magician" }, // IDS_CLASS_KINDOF_PET_WIZARD
            { 1314, "Healing Priest" }, // IDS_CLASS_KINDOF_HEAL_PRIEST
            { 1315, "Cursing Priest" }, // IDS_CLASS_KINDOF_CURSE_PRIEST
            { 1401, "Rogue" }, // IDS_CLASS_EL_ASSASIN (Kasar Hood)
            { 1402, "Warrior" }, // IDS_CLASS_EL_BLADE (Blade)
            { 1403, "Priest" }, // IDS_CLASS_EL_CLERIC (Cleric)
            { 1404, "Priest" }, // IDS_CLASS_EL_DRUID (Paladin)
            { 1405, "Mage" }, // IDS_CLASS_EL_ENCHANTER (Arch Mage)
            { 1406, "Mage" }, // IDS_CLASS_EL_MAGE (Mage)
            { 1407, "Warrior" }, // IDS_CLASS_EL_PROTECTOR (Blade Master)
            { 1408, "Rogue" }, // IDS_CLASS_EL_RANGER (Ranger)
            { 1409, "Warrior" }, // IDS_CLASS_KA_BERSERKER (Berserker)
            { 1410, "Priest" }, // IDS_CLASS_KA_DARKPRIEST (Shadow Knight)
            { 1411, "Warrior" }, // IDS_CLASS_KA_GUARDIAN (Berserker Hero)
            { 1412, "Rogue" }, // IDS_CLASS_KA_HUNTER (Hunter)
            { 1413, "Mage" }, // IDS_CLASS_KA_NECROMANCER (Elemental Lord)
            { 1414, "Rogue" }, // IDS_CLASS_KA_PENETRATOR (Shadow Vain)
            { 1415, "Priest" }, // IDS_CLASS_KA_SHAMAN (Shaman)
            { 1416, "Mage" }, // IDS_CLASS_KA_SORCERER (Sorcerer)
            { 1417, "Priest" }, // IDS_CLASS_PRIEST
            { 1418, "Rogue" }, // IDS_CLASS_ROGUE
            { 1419, "Unconfirmed Specialty" }, // IDS_CLASS_UNKNOWN
            { 1420, "Warrior" }, // IDS_CLASS_WARRIOR
            { 1421, "Magician" }, // IDS_CLASS_WIZARD
            { 1501, "Character deletion has been disabled currently" }, // IDS_CONFIRM_DELETE_CHR
            { 1502, "Are you sure you want to exit?" }, // IDS_CONFIRM_EXIT_GAME
            { 1601, "Disconnected from server" }, // IDS_CONNECTION_CLOSED
            { 1701, "You cannot buy more than 9,999 items at once." }, // IDS_COUNTABLE_ITEM_BUY_FAIL
            { 1702, "You don't have enough Coins." }, // IDS_COUNTABLE_ITEM_BUY_NOT_ENOUGH_MONEY
            { 1703, "You cannot carry more than 9,999 items at once." }, // IDS_COUNTABLE_ITEM_GET_MANY
            { 1704, "You cannot have more than 9,999 items." }, // IDS_COUNTABLE_ITEM_TOO_MAMY
            { 1705, "You cannot buy more than 500 items at once." }, // IDS_SMALL_COUNTABLE_ITEM_BUY_FAIL
            { 1706, "You cannot carry more than 500 items at once." }, // IDS_SMALL_COUNTABLE_ITEM_GET_MANY
            { 1707, "You cannot have more than 500 items." }, // IDS_SMALL_COUNTABLE_ITEM_TOO_MAMY
            { 1708, "You cannot trade or pick up items because you have either exceeded the possible quantity or the weight." }, // IDS_ITEM_TOOMANY_OR_HEAVY
            { 1801, "The Castle Gate has been closed" }, // IDS_DOOR_CLOSED
            { 1802, "The Castle Gate has opened" }, // IDS_DOOR_OPENED
            { 1901, "Failed creating character" }, // IDS_ERR_CHARACTER_CREATE
            { 1902, "Failed creating Database" }, // IDS_ERR_DB_CREATE
            { 1903, "You cannot teleport back to town when you have half the HP or less" }, // IDS_ERR_GOTO_TOWN_OUT_OF_HP
            { 1904, "Please select a specialty." }, // IDS_ERR_INVALID_CLASS
            { 1905, "Please enter your character ID." }, // IDS_ERR_INVALID_NAME
            { 1906, "You cannot use this character ID." }, // IDS_ERR_INVALID_NAME_HAS_SPECIAL_LETTER
            { 1907, "The selected nation and the race does not match." }, // IDS_ERR_INVALID_NATION_RACE
            { 1908, "Please select a race." }, // IDS_ERR_INVALID_RACE
            { 1909, "You need to have a name in order to create a Knights" }, // IDS_ERR_KNIGHTS_CREATE_FAILED_NAME_EMPTY
            { 1910, "This race is not available yet." }, // IDS_ERR_NOT_SUPPORTED_RACE
            { 1911, "You cannot create anymore characters." }, // IDS_ERR_NO_MORE_CHARACTER
            { 1912, "This ID is already used on another character." }, // IDS_ERR_OVERLAPPED_ID
            { 1913, "There are stat points still remaining." }, // IDS_ERR_REMAIN_BONUS_POINT
            { 1914, "You are too far away from the object." }, // IDS_ERR_REQUEST_OBJECT_EVENT_SO_FAR
            { 1915, "Unknown error." }, // IDS_ERR_UNKNOWN
            { 2001, "Are you sure you want to exit?" }, // IDS_EXIT
            { 2101, "*** Current concurrent user : %d ***" }, // IDS_FMT_CONCURRENT_USER_COUNT
            { 2201, "Arial" }, // IDS_FONT_BALLOON
            { 2202, "Arial" }, // IDS_FONT_ID
            { 2203, "Arial" }, // IDS_FONT_INFO
            { 2301, "You cannot pick up the item because your item inventory is full." }, // IDS_INV_ITEM_FULL
            { 2401, "Craft item." }, // IDS_ITEM_ATTRIB_CRAFT
            { 2402, "Regular item." }, // IDS_ITEM_ATTRIB_GENERAL
            { 2403, "Rare item." }, // IDS_ITEM_ATTRIB_LAIR
            { 2404, "Magic item." }, // IDS_ITEM_ATTRIB_MAGIC
            { 2405, "Unique item." }, // IDS_ITEM_ATTRIB_UNIQUE
            { 2406, "Upgrade item." }, // IDS_ITEM_ATTRIB_UPGRADE
            { 2501, "Necklace" }, // IDS_ITEM_CLASS_AMULET
            { 2502, "Magician Armor" }, // IDS_ITEM_CLASS_ARMOR_MAGE
            { 2503, "Priest Armor" }, // IDS_ITEM_CLASS_ARMOR_PRIEST
            { 2504, "Rogue Armor" }, // IDS_ITEM_CLASS_ARMOR_ROGUE
            { 2505, "Warrior Armor" }, // IDS_ITEM_CLASS_ARMOR_WARRIOR
            { 2506, "Arrow" }, // IDS_ITEM_CLASS_ARROW
            { 2507, "Axe" }, // IDS_ITEM_CLASS_AXE
            { 2508, "Two-handed Axe" }, // IDS_ITEM_CLASS_AXE_2H
            { 2509, "Belt" }, // IDS_ITEM_CLASS_BELT
            { 2510, "Bow" }, // IDS_ITEM_CLASS_BOW
            { 2511, "Crossbow" }, // IDS_ITEM_CLASS_BOW_CROSS
            { 2512, "Long Bow" }, // IDS_ITEM_CLASS_BOW_LONG
            { 2513, "Lune Item" }, // IDS_ITEM_CLASS_CHARM
            { 2514, "Dagger" }, // IDS_ITEM_CLASS_DAGGER
            { 2515, "Earring" }, // IDS_ITEM_CLASS_EARRING
            { 2516, "Others" }, // IDS_ITEM_CLASS_ETC
            { 2517, "Javelin" }, // IDS_ITEM_CLASS_JAVELIN
            { 2518, "Jewelry" }, // IDS_ITEM_CLASS_JEWEL
            { 2519, "IDS_ITEM_CLASS_LAUNCHER" }, // IDS_ITEM_CLASS_LAUNCHER
            { 2520, "Club" }, // IDS_ITEM_CLASS_MACE
            { 2521, "Two-handed Club" }, // IDS_ITEM_CLASS_MACE_2H
            { 2522, "Long Spear" }, // IDS_ITEM_CLASS_POLEARM
            { 2523, "Potion" }, // IDS_ITEM_CLASS_POTION
            { 2524, "Ring" }, // IDS_ITEM_CLASS_RING
            { 2525, "Scroll" }, // IDS_ITEM_CLASS_SCROLL
            { 2526, "Shield" }, // IDS_ITEM_CLASS_SHIELD
            { 2527, "Spear" }, // IDS_ITEM_CLASS_SPEAR
            { 2528, "Staff" }, // IDS_ITEM_CLASS_STAFF
            { 2529, "One-handed Sword" }, // IDS_ITEM_CLASS_SWORD
            { 2530, "Two-handed Sword" }, // IDS_ITEM_CLASS_SWORD_2H
            { 2601, "You've exceeded your possible carrying weight." }, // IDS_ITEM_WEIGHT_OVERFLOW
            { 2701, "Request for Joining Knights has been declined." }, // IDS_KNIGHTS_ADMIT_FAILED
            { 2702, "Successfully admitted into the Knights" }, // IDS_KNIGHTS_ADMIT_SUCCESS
            { 2703, "Failed to be appointed as a Knights Leader" }, // IDS_KNIGHTS_APPOINT_CHIEF_FAILED
            { 2704, "Successfully appointed as a Knights Leader" }, // IDS_KNIGHTS_APPOINT_CHIEF_SUCCESS
            { 2705, "Failed to be appointed as Officer" }, // IDS_KNIGHTS_APPOINT_OFFICER_FAILED
            { 2706, "Successfully appointed as a Officer" }, // IDS_KNIGHTS_APPOINT_OFFICER_SUCCESS
            { 2707, "Failed to be appointed as Assistant Leader" }, // IDS_KNIGHTS_APPOINT_VICECHIEF_FAILED
            { 2708, "Successfully appointed as Assistant Leader" }, // IDS_KNIGHTS_APPOINT_VICECHIEF_SUCCES
            { 2709, "Failed to create the Knights" }, // IDS_KNIGHTS_CREATE_FAILED
            { 2710, "Succesfully created the Knights" }, // IDS_KNIGHTS_CREATE_SUCCESS
            { 2711, "Do you want to disband the Knights?" }, // IDS_KNIGHTS_DESTROY_CONFIRM
            { 2712, "Failed to disband the Knights" }, // IDS_KNIGHTS_DESTROY_FAILED
            { 2713, "Successfully disbanded the Knights" }, // IDS_KNIGHTS_DESTROY_SUCCESS
            { 2714, "Leader" }, // IDS_KNIGHTS_DUTY_CHIEF
            { 2715, "Member" }, // IDS_KNIGHTS_DUTY_KNIGHT
            { 2716, "Staff Officer" }, // IDS_KNIGHTS_DUTY_OFFICER
            { 2717, "Under disciplinary punishment" }, // IDS_KNIGHTS_DUTY_PUNISH
            { 2718, "Apprentice" }, // IDS_KNIGHTS_DUTY_TRAINEE
            { 2719, "none" }, // IDS_KNIGHTS_DUTY_UNKNOWN
            { 2720, "Assistant Leader" }, // IDS_KNIGHTS_DUTY_VICECHIEF
            { 2721, "Failed to join the Knights" }, // IDS_KNIGHTS_JOIN_FAILED
            { 2722, "Successfully joined the Knights" }, // IDS_KNIGHTS_JOIN_SUCCESS
            { 2723, "Failed to displine a member of the Knights" }, // IDS_KNIGHTS_PUNISH_FAILED
            { 2724, "Successfully disciplined a member of the Knights" }, // IDS_KNIGHTS_PUNISH_SUCCESS
            { 2725, "Failed to decline a request to join the Knights" }, // IDS_KNIGHTS_REJECT_FAILED
            { 2726, "Successfully declined a request to join the Knights" }, // IDS_KNIGHTS_REJECT_SUCCESS
            { 2727, "Failed to ban a member of the Knights" }, // IDS_KNIGHTS_REMOVE_MEMBER_FAILED
            { 2728, "Successfully banned a member of the Knights" }, // IDS_KNIGHTS_REMOVE_MEMBER_SUCCESS
            { 2729, "Do you want to quit the Knights?" }, // IDS_KNIGHTS_WITHDRAW_CONFIRM
            { 2730, "Failed to quit the Knights" }, // IDS_KNIGHTS_WITHROW_FAILED
            { 2731, "Successfully quitted the Knights" }, // IDS_KNIGHTS_WITHROW_SUCCESS
            { 2801, "The lever has been activated." }, // IDS_LEVER_ACTIVATE
            { 2802, "The lever has been deactivated." }, // IDS_LEVER_DEACTIVATE
            { 2901, "Log in failed.  Please contact customer service." }, // IDS_LOGIN_FAILED
            { 2902, "Log in failed.  Please try again." }, // IDS_LOGIN_ERR_ALREADY_CONNECTED_ACCOUNT
            { 2903, "No such registered ID" }, // IDS_NOACCOUNT_RETRY_MGAMEID
            { 2904, "The ID doesn't exist on MGame either." }, // IDS_NO_MGAME_ACCOUNT
            { 2905, "Failed logging into the %s server. (%d)" }, // IDS_FMT_CONNECT_ERROR
            { 2906, "Successfully connected to %s server but failed logging into the game. (%d)" }, // IDS_FMT_GAME_SERVER_LOGIN_ERROR
            { 2907, "Your account is currently blocked. Please contact customer support." }, // IDS_SERVER_CONNECT_FAIL
            { 2908, "There is an error in the selected server." }, // IDS_CURRENT_SERVER_ERROR
            { 2909, "Connection failed." }, // IDS_CONNECT_FAIL
            { 3001, "Could not attack because you're facing the wrong direction or the target is too far." }, // IDS_MSG_ATTACK_DISABLE
            { 3002, "Beginning attack on %s" }, // IDS_MSG_ATTACK_START
            { 3003, "Stop Attack" }, // IDS_MSG_ATTACK_STOP
            { 3004, "Skill Failed - Not enough MP" }, // IDS_MSG_CASTING_FAIL_LACK_MP
            { 3007, "Earned %d Experience Points" }, // IDS_MSG_FMT_EXP_GET
            { 3008, "Lost %d Experience Points" }, // IDS_MSG_FMT_EXP_LOST
            { 3009, "%d HP Damage" }, // IDS_MSG_FMT_HP_LOST
            { 3010, "%d HP Recovered" }, // IDS_MSG_FMT_HP_RECOVER
            { 3011, "%d MP Recovered" }, // IDS_MSG_FMT_MP_RECOVER
            { 3012, "%d MP Used" }, // IDS_MSG_FMT_MP_USE
            { 3013, "%d SP Recovered" }, // IDS_MSG_FMT_SP_RECOVER
            { 3014, "%d SP Used" }, // IDS_MSG_FMT_SP_USE
            { 3015, "%s Missed." }, // IDS_MSG_FMT_TARGET_ATTACK_FAILED
            { 3016, "%s received %d damage" }, // IDS_MSG_FMT_TARGET_HP_LOST
            { 3017, "%s received %d HP" }, // IDS_MSG_FMT_TARGET_HP_RECOVER
            { 3018, "You cannot equip this item.  This item is designed for a different race." }, // IDS_MSG_VALID_CLASSNRACE_INVALID_RACE
            { 3019, "You cannot equip this item because you don't have enough Magic Power stat point" }, // IDS_MSG_VALID_CLASSNRACE_LOW_CHA
            { 3020, "You cannot equip this item because you don't have enough Dexterity stat point" }, // IDS_MSG_VALID_CLASSNRACE_LOW_DEX
            { 3021, "You cannot equip this item because you don't have enough Intelligence stat point" }, // IDS_MSG_VALID_CLASSNRACE_LOW_INT
            { 3022, "You cannot equip this item because your level is too low" }, // IDS_MSG_VALID_CLASSNRACE_LOW_LEVEL
            { 3023, "You cannot equip this item because you don't have enough Strength stat points." }, // IDS_MSG_VALID_CLASSNRACE_LOW_POWER
            { 3024, "You cannot equip this item because of your class." }, // IDS_MSG_VALID_CLASSNRACE_LOW_RANK
            { 3025, "You cannot equip this item because you don't have enough Health stat points." }, // IDS_MSG_VALID_CLASSNRACE_LOW_STR
            { 3026, "You cannot equip this item because your title is too low." }, // IDS_MSG_VALID_CLASSNRACE_LOW_TITLE
            { 3027, "The number of users has surpassed its maximum limit allowed for a specific zone" }, // IDS_MSG_CONCURRENT_USER_OVERFLOW
            { 3028, "You cannot equip this item.  This item is not designed for your character's specialty." }, // IDS_MSG_VALID_CLASSNRACE_INVALID_CLASS
            { 3101, "El Morad" }, // IDS_NATION_ELMORAD
            { 3102, "Karus" }, // IDS_NATION_KARUS
            { 3103, "Unconfirmed nation" }, // IDS_NATION_UNKNOWN
            { 3201, "This is the stat that affects the character's power for magic attacks." }, // IDS_NEWCHR_MAP
            { 3202, "This stat increases character's dodging ability and affects the power of arrow attacks for rogues." }, // IDS_NEWCHR_DEX
            { 3203, "The barbarians are warriors with strong physiques from the north.  They can only become a warrior." }, // IDS_NEWCHR_EL_BABA
            { 3204, "El Moradian female characters possess strong Magic Power and high Intelligence." }, // IDS_NEWCHR_EL_FEMALE
            { 3205, "The Magicians can become a Mage which uses 4 basic elements to perform attack magic or they can become an Enchanter, the master of mind control." }, // IDS_NEWCHR_EL_MAGE
            { 3206, "El Moradian males have balanced Strength and Intelligence which makes them fit for any kind of job." }, // IDS_NEWCHR_EL_MALE
            { 3207, "The Priests can become a Cleric to heal friends or become a Druid to cast a curse on the enemy or increase ally's stats." }, // IDS_NEWCHR_EL_PRIEST
            { 3208, "The Rogues can become an Assassin to sneak up to the enemy and inflict a critical damage, or they can become a Ranger that can attack enemies from far away with bows and spears." }, // IDS_NEWCHR_EL_ROGUE
            { 3209, "The Warriors can become a Blade that uses variety of weapons to inflict critical damage, or they can become a Protector with high defense ability to protect ally's magicians and priests." }, // IDS_NEWCHR_EL_WARRIOR
            { 3210, "This stat affects the amount of Mental Power (MP) for Magicians and Priests." }, // IDS_NEWCHR_INT
            { 3211, "Arch Tuarek is a physically strong race who are fit to become a warrior." }, // IDS_NEWCHR_KA_ARKTUAREK
            { 3212, "The Magicians can become a Sorcerer which uses 4 basic elements to perform attack magic or they can become a Necromancer, the master of the dead." }, // IDS_NEWCHR_KA_MAGE
            { 3213, "The Priests can become a Shaman that heals its ally or become a Dark Priest that can use cursing skills." }, // IDS_NEWCHR_KA_PRIEST
            { 3214, "퓨리 투아렉은 순수하게 정화된 정신 에너지를 이용해 치료 마법과 저주 마법을 구사하는 카루스의 여성 캐릭터로 법사와 사제의 직업을 선택할 수 있습니다." }, // IDS_NEWCHR_KA_PURITUAREK
            { 3215, "The Rogues can become an Assassin to sneak up to the enemy and inflict a critical damage, or they can become a Hunter that can attack enemies from far away with bows and spears." }, // IDS_NEWCHR_KA_ROGUE
            { 3216, "Tuareks have balanced Strength and Intelligence which makes them fit for any kind of job." }, // IDS_NEWCHR_KA_TUAREK
            { 3217, "The Warriors can become a Berserker that uses variety of weapons to inflict critical damage, or they can become a Guardian with high defense ability to protect ally's magicians and priests." }, // IDS_NEWCHR_KA_WARRIOR
            { 3218, "Wrinkle Tuareks have strong mental powers. Wrinkle Tuareks can only become a Magician." }, // IDS_NEWCHR_KA_WRINKLETUAREK
            { 3219, "This stat affects the attack/defense power of a character that uses a weapon." }, // IDS_NEWCHR_POW
            { 3220, "This stat affects the amount of character's Health Point (HP)." }, // IDS_NEWCHR_STA
            { 3301, "A great blacksmith like me is hard to find.  Tell me if you need anything…" }, // IDS_NPCEVENT_TITLE_REPAIR
            { 3302, "I have many items.  Would you like to take a look at them?" }, // IDS_NPC_EVENT_TITLE_TRADE
            { 3401, "Are you sure you want to disband the party?" }, // IDS_PARTY_CONFIRM_DESTROY
            { 3402, "Would you like to ban %s form the party?" }, // IDS_PARTY_CONFIRM_DISCHARGE
            { 3403, "Would you like to quit the party?" }, // IDS_PARTY_CONFIRM_LEAVE
            { 3404, "The party has been disbanded." }, // IDS_PARTY_DESTROY
            { 3405, "has joined the party." }, // IDS_PARTY_INSERT
            { 3406, "The invitation to the party has been declined." }, // IDS_PARTY_INSERT_ERR
            { 3407, "You cannot form a party with a user from the other nation." }, // IDS_PARTY_INSERT_ERR_INVALID_NATION
            { 3408, "You cannot form a party because of the Level difference." }, // IDS_PARTY_INSERT_ERR_LEVEL_DIFFERENCE
            { 3409, "The invitation to the party has been declined." }, // IDS_PARTY_INSERT_ERR_REJECTED
            { 3411, "Player was invited into the party. Waiting for a response." }, // IDS_PARTY_INVITE
            { 3412, "could not be invited into the party." }, // IDS_PARTY_INVITE_FAILED
            { 3413, "%s got %s" }, // IDS_PARTY_ITEM_GET
            { 3414, "You've quit the party." }, // IDS_PARTY_LEAVE
            { 3415, "has invited you to join the party. Will you join?" }, // IDS_PARTY_PERMIT
            { 3501, "%s has sent a request for a trade to %s. Please wait for a reply." }, // IDS_PERSONAL_TRADE_FMT_WAIT
            { 3502, "%s has received a request for a trade from %s. Will you accept?" }, // IDS_PERSONAL_TRADE_PERMIT
            { 3503, "is requesting to trade." }, // IDS_PERSONAL_TRADE_REQUEST
            { 3504, "A request for a trade has been received from another user while you were trading." }, // IDS_PER_TRADEING_OTHER
            { 3601, "All race" }, // IDS_RACE_ALL
            { 3602, "Barbarian" }, // IDS_RACE_EL_BABARIAN
            { 3603, "Male El Moradian" }, // IDS_RACE_EL_MAN
            { 3604, "Female El Moradian" }, // IDS_RACE_EL_WOMEN
            { 3605, "Arch Tuarek" }, // IDS_RACE_KA_ARKTUAREK
            { 3606, "Puri Tuarek" }, // IDS_RACE_KA_PURITUAREK
            { 3607, "Tuarek" }, // IDS_RACE_KA_TUAREK
            { 3608, "Wrinkle Tuarek" }, // IDS_RACE_KA_WRINKLETUAREK
            { 3609, "Unconfirmed race" }, // IDS_RACE_UNKNOWN
            { 3701, "Press OK to teleport back to the re-spawn point." }, // IDS_REGENERATION
            { 3801, "You don't have enough money for the repair." }, // IDS_REPAIR_LACK_GOLD
            { 3901, "Would you like to designate this place as your re-spawn point?" }, // IDS_REQUEST_BINDPOINT
            { 4002, "Casting failed" }, // IDS_SKILL_FAIL_CASTING
            { 4003, "You cannot user this Skill/Magic." }, // IDS_SKILL_FAIL_DIFFURENTCLASS
            { 4004, "%s failed" }, // IDS_SKILL_FAIL_EFFECTING
            { 4005, "Skill Failed - Improper item" }, // IDS_SKILL_FAIL_INVALID_ITEM
            { 4006, "Skill Failed - Not enough HP" }, // IDS_SKILL_FAIL_LACK_HP
            { 4007, "Skill Failed - Not enough item" }, // IDS_SKILL_FAIL_LACK_ITEM
            { 4008, "Skill Failed - Not enough SP" }, // IDS_SKILL_FAIL_LACK_SP
            { 4009, "Skill Failed - Too far" }, // IDS_SKILL_FAIL_SOFAR
            { 4010, "Skill Failed - Blocked by an object" }, // IDS_SKILL_HEALING_FAIL_SOFAR
            { 4101, "You cannot distribute your skill points because you haven't picked your specialty yet." }, // IDS_SKILL_POINT_BEFORE_CLASS_CHANGE
            { 4102, "There's no remaining skill point." }, // IDS_SKILL_POINT_EXTRA_NOT_EXIST
            { 4103, "This skill is not available." }, // IDS_SKILL_POINT_NOT_YET
            { 4201, "%s heals you %d HP" }, // IDS_SKILL_SUCCESS_HEALING_FROM
            { 4202, "%s received %d HP" }, // IDS_SKILL_SUCCESS_HEALING_TO
            { 4301, "Required Item : Double Hand" }, // IDS_SKILL_TOOLTIP_DOUBLE
            { 4302, "Required Item : Dual Hand" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_DUAL
            { 4303, "Required Item : All Weapons" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID1
            { 4304, "Required Item : Launcher" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID10
            { 4305, "Required Item : Staff" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID11
            { 4306, "Required Item : Arrow" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID12
            { 4307, "Required Item : Javelin" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID13
            { 4308, "Required Item : Warrior Armor" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID14
            { 4309, "Required Item : Rogue Armor" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID15
            { 4310, "Required Item : Magician Armor" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID16
            { 4311, "Required Item : Priest Armor" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID17
            { 4312, "Required Item : Dagger" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID2
            { 4313, "Required Item : Sword" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID3
            { 4314, "Required Item : Ax" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID4
            { 4315, "Required Item : Striking Weapon" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID5
            { 4316, "Required Item : Spear" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID6
            { 4317, "Required Item : Shield" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID7
            { 4318, "Required Item : Bow" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID8
            { 4319, "Required Item : Longbow" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_ID9
            { 4320, "Doesn't use MP" }, // IDS_SKILL_TOOLTIP_NO_MANA
            { 4321, "Item Consumed : %s" }, // IDS_SKILL_TOOLTIP_USE_ITEM_EXIST
            { 4322, "No item consumed" }, // IDS_SKILL_TOOLTIP_USE_ITEM_NO
            { 4323, "MP consumed : %d" }, // IDS_SKILL_TOOLTIP_USE_MANA
            { 4324, "Required Level : %d" }, // IDS_SKILL_TOOLTIP_NEED_LEVEL
            { 4325, "Required Skill Point : %d" }, // IDS_SKILL_TOOLTIP_NEED_SKILL_PT
            { 4328, "No basic item" }, // IDS_SKILL_TOOLTIP_NEED_ITEM_NO
            { 4329, "Required item : %s" }, // IDS_SKILL_TOOLTIP_ITEM_NEED
            { 4330, "No required item" }, // IDS_SKILL_TOOLTIP_ITEM_NO
            { 4401, "The value of a Skill Point cannot be greater than your level." }, // IDS_SKILL_UP_INVALID
            { 4402, "Using %s" }, // IDS_SKILL_USE
            { 4502, "Attack Speed : Fast" }, // IDS_TOOLTIP_ATTACKINT_FAST
            { 4503, "Attack Speed : Normal" }, // IDS_TOOLTIP_ATTACKINT_NORMAL
            { 4504, "Attack Speed : Slow" }, // IDS_TOOLTIP_ATTACKINT_SLOW
            { 4505, "Attack Speed : Very Fast" }, // IDS_TOOLTIP_ATTACKINT_VERYFAST
            { 4506, "Attack Speed : Very Slow" }, // IDS_TOOLTIP_ATTACKINT_VERYSLOW
            { 4507, "Effective Range : %.2f" }, // IDS_TOOLTIP_ATTACKRANGE
            { 4508, "Flame Damage : %d" }, // IDS_TOOLTIP_ATTRMAGIC1
            { 4509, "Glacier Damage : %d" }, // IDS_TOOLTIP_ATTRMAGIC2
            { 4510, "Lightning Damage : %d" }, // IDS_TOOLTIP_ATTRMAGIC3
            { 4511, "Poison Damage : %d" }, // IDS_TOOLTIP_ATTRMAGIC4
            { 4512, "HP Absorbed : %d" }, // IDS_TOOLTIP_ATTRMAGIC5
            { 4513, "MP Damage : %d" }, // IDS_TOOLTIP_ATTRMAGIC6
            { 4514, "MP Absorbed : %d" }, // IDS_TOOLTIP_ATTRMAGIC7
            { 4515, "Increase Dodging Rate by %d%%" }, // IDS_TOOLTIP_AVOIDRATE_OVER
            { 4516, "Decrease Dodging Rate by %d%%" }, // IDS_TOOLTIP_AVOIDRATE_UNDER
            { 4517, "Magic Power Bonus : %d" }, // IDS_TOOLTIP_BONUSMAGICATTACK
            { 4518, "Dexterity Bonus : %d" }, // IDS_TOOLTIP_BONUSDEX
            { 4519, "HP Bonus : %d" }, // IDS_TOOLTIP_BONUSHP
            { 4520, "Intelligence Bonus : %d" }, // IDS_TOOLTIP_BONUSINT
            { 4521, "Strength Bonus : %d" }, // IDS_TOOLTIP_BONUSSTR
            { 4522, "MP Bonus : %d" }, // IDS_TOOLTIP_BONUSWIZ
            { 4523, "Purchasing Price : %s" }, // IDS_TOOLTIP_BUY_PRICE
            { 4524, "Current Durability : %d" }, // IDS_TOOLTIP_CUR_DURABILITY
            { 4525, "Attack Power : %d" }, // IDS_TOOLTIP_DAMAGE
            { 4526, "Defense Ability : %d" }, // IDS_TOOLTIP_DEFENSE
            { 4527, "Defense Ability (Arrow) : %d" }, // IDS_TOOLTIP_DEFENSE_RATE_ARROW
            { 4528, "Defense Ability (Ax) : %d" }, // IDS_TOOLTIP_DEFENSE_RATE_AXE
            { 4529, "Defense Ability (Club) : %d" }, // IDS_TOOLTIP_DEFENSE_RATE_BLOW
            { 4530, "Defense Ability (Dagger) : %d" }, // IDS_TOOLTIP_DEFENSE_RATE_DAGGER
            { 4531, "Defense Ability (Spear) : %d" }, // IDS_TOOLTIP_DEFENSE_RATE_SPEAR
            { 4532, "Defense Ability (Sword) : %d" }, // IDS_TOOLTIP_DEFENSE_RATE_SWORD
            { 4533, "Coin" }, // IDS_TOOLTIP_GOLD
            { 4534, "Increase Attack Power by %d%%" }, // IDS_TOOLTIP_HITRATE_OVER
            { 4535, "Decrease Attack Power by %d%%" }, // IDS_TOOLTIP_HITRATE_UNDER
            { 4536, "Max Durability : %d" }, // IDS_TOOLTIP_MAX_DURABILITY
            { 4537, "Required Magic Power : %d %s" }, // IDS_TOOLTIP_NEEDMAGICATTACK
            { 4538, "Required Dexterity : %d %s" }, // IDS_TOOLTIP_NEEDDEXTERITY
            { 4540, "Required Intelligence : %d %s" }, // IDS_TOOLTIP_NEEDINTELLI
            { 4541, "Required Level : %d" }, // IDS_TOOLTIP_NEEDLEVEL
            { 4542, "Required Class : %d" }, // IDS_TOOLTIP_NEEDRANK
            { 4543, "Required Health : %d %s" }, // IDS_TOOLTIP_NEEDSTAMINA
            { 4544, "Required Strength : %d %s" }, // IDS_TOOLTIP_NEEDSTRENGTH
            { 4545, "Required Title : %s" }, // IDS_TOOLTIP_NEEDTITLE
            { 4546, "Resistance to Curse : %d" }, // IDS_TOOLTIP_REGISTCURSE
            { 4547, "Resistance to Lightning : %d" }, // IDS_TOOLTIP_REGISTELEC
            { 4548, "Resistance to Flame : %d" }, // IDS_TOOLTIP_REGISTFIRE
            { 4549, "Resistance to Glacier : %d" }, // IDS_TOOLTIP_REGISTICE
            { 4550, "Resistance to Magic : %d" }, // IDS_TOOLTIP_REGISTMAGIC
            { 4551, "Resistance to Poison : %d" }, // IDS_TOOLTIP_REGISTPOISON
            { 4552, "Selling Price : %s" }, // IDS_TOOLTIP_SELL_PRICE
            { 4553, "Weight : %.2f" }, // IDS_TOOLTIP_WEIGHT
            { 4554, "(Reduce)" }, // IDS_TOOLTIP_REDUCE
            { 4555, "Repel Physical Attack : %d" }, // IDS_TOOLTIP_REPEL_PHYSICAL
            { 4556, "[%s] Remaining Time : %d minutes" }, // IDS_TOOLTIP_RENTAL_TIME
            { 4558, "Required Level : %d ~ %d" }, // IDS_TOOLTIP_NEEDLEVEL_RANGE
            { 4559, "Required Title : %s" }, // IDS_TOOLTIP_NEEDTITLE2
            { 4560, "Item Grade : %d Grade" }, // IDS_TOOLTIP_GRADE
            { 4561, "Unique" }, // IDS_TOOLTIP_UNIQUE
            { 4562, "Item Grade : low Class" }, // IDS_TOOLTIP_LOW_CLASS
            { 4563, "Item Grade : middle Class" }, // IDS_TOOLTIP_MIDDLE_CLASS
            { 4564, "Item Grade : high Class" }, // IDS_TOOLTIP_HIGH_CLASS
            { 4601, "Speed Hacking has been tried." }, // IDS_TRY_SPEED_HACKING
            { 4701, "http://www.knightonlineworld.com" }, // IDS_URL_JOIN
            { 4801, "This client is version %.3f. You'll need to have version %.3f in order to log in to the server." }, // IDS_VERSION_CONFIRM
            { 4802, "Health Bonus : %d" }, // IDS_TOOLTIP_BONUSSTA
            { 4803, "This is an outdated version. Please install the newest client." }, // IDS_VERSION_CONFIRM_TW
            { 5001, "Recover rapidly by sitting down - 'C'" }, // IDS_HELP_TIP1
            { 5002, "You can move using your mouse in the 'quarter-view' mode." }, // IDS_HELP_TIP2
            { 5003, "Toggle Auto Walk/Run - 'E'" }, // IDS_HELP_TIP3
            { 5004, "When you want to teleport back to town - Type'/town'" }, // IDS_HELP_TIP4
            { 5005, "To find the nearest enemy/monster - Press the 'Z' key" }, // IDS_HELP_TIP5
            { 5006, "Using Skill : Drag the skill icon to the shortcut key window." }, // IDS_HELP_TIP6
            { 5007, "Designate re-spawn spot : Right-click on the Resurrection Stone." }, // IDS_HELP_TIP7
            { 5008, "Visit the blacksmith to increase the durability of an item." }, // IDS_HELP_TIP8
            { 5009, "Item Storage : Right-click on the Inn-Keeper." }, // IDS_HELP_TIP9
            { 5010, "Selecting Specialty : You can select your specialty when you reach level 10." }, // IDS_HELP_TIP10
            { 5011, "Pick up item : Click on the dead monster." }, // IDS_HELP_TIP11
            { 5012, "Walk/Run : Press the 'Y' key" }, // IDS_HELP_TIP12
            { 5013, "Toggle Attack : Press the 'R' key or double click on the target." }, // IDS_HELP_TIP13
            { 5014, "Toggle View : shortcut key 'F9'" }, // IDS_HELP_TIP14
            { 5015, "You can move your character by pressing 'W,A,S,D's keys or the arrow keys." }, // IDS_HELP_TIP15
            { 5016, "Using Skill : Press the skill icon placed on the shortcut key window or press '1~8'" }, // IDS_HELP_TIP16
            { 5017, "You can level up faster if you form a party." }, // IDS_HELP_TIP17
            { 5018, "Display request for party : Type'/Seeking_party'" }, // IDS_HELP_TIP18
            { 5019, "Invite into a party : Type '/party'" }, // IDS_HELP_TIP19
            { 5020, "Trade Item : Select a character and type '/trade'" }, // IDS_HELP_TIP20
            { 5021, "Party Chat : Click on the 'Party Chat' button on the chat window" }, // IDS_HELP_TIP21
            { 5022, "General Chat : Click on the 'General Chat' button on the chat window" }, // IDS_HELP_TIP22
            { 5023, "Gaining Stat Points : Gain 3 stat points every time you level up." }, // IDS_HELP_TIP23
            { 5024, "Gaining Skill Points : You can gain 2 skill points every time you level up once you've reached level 10." }, // IDS_HELP_TIP24
            { 5025, "Destroy Item : Drag and drop the item you wish to destroy on to the red mark on the inventory window." }, // IDS_HELP_TIP25
            { 5026, "View Description of the Skill : Put your cursor over the skill icon." }, // IDS_HELP_TIP26
            { 5027, "Private Message : Type '/pm' ID" }, // IDS_HELP_TIP27
            { 5028, "View Mini-map : Press 'N'.  The red and orange dots are enemies." }, // IDS_HELP_TIP28
            { 5029, "Flip through shortcut key window : 'PageUp/PageDown'" }, // IDS_HELP_TIP29
            { 5030, "Use the 'Home / End' keys to change views in the quarter-view mode." }, // IDS_HELP_TIP30
            { 5031, "Take 5 Silk Bundles to the Proconsul standing near the accessory vendor and he'll give you a weapon." }, // IDS_HELP_TIP31
            { 5032, "Lock on the closest NPC - 'B'" }, // IDS_HELP_TIP32
            { 5033, "View Overall Map : 'M'" }, // IDS_HELP_TIP33
            { 5034, "[Knight Online Tip of the Day!]" }, // IDS_HELP_TIP_ALL
            { 6033, "Please enter the amount." }, // IDS_EDIT_BOX_GOLD
            { 6034, "Please enter the quantity." }, // IDS_EDIT_BOX_COUNT
            { 6035, "Basic skills that you get before you choose a specialty." }, // IDS_SKILL_INFO_BASE
            { 6036, "Learn various attack skills." }, // IDS_SKILL_INFO_BLADE0
            { 6037, "Learn various defense skills." }, // IDS_SKILL_INFO_BLADE1
            { 6038, "Increases Warrior's mental power." }, // IDS_SKILL_INFO_BLADE2
            { 6039, "Learn the Master Skills of various weapons." }, // IDS_SKILL_INFO_BLADE3
            { 6044, "Learn various archery skills." }, // IDS_SKILL_INFO_RANGER0
            { 6045, "Learn the various techniques of assassination." }, // IDS_SKILL_INFO_RANGER1
            { 6046, "Learn the various techniques of exploring." }, // IDS_SKILL_INFO_RANGER2
            { 6047, "Learn the Master Skills of Archery and Assassination." }, // IDS_SKILL_INFO_RANGER3
            { 6052, "Learn various magic that uses flame." }, // IDS_SKILL_INFO_MAGE0
            { 6053, "Learn various magic that uses Glacier." }, // IDS_SKILL_INFO_MAGE1
            { 6054, "Learn various magic that uses lightning(Lightning)." }, // IDS_SKILL_INFO_MAGE2
            { 6055, "Learn the Master Skills of elemental magic." }, // IDS_SKILL_INFO_MAGE3
            { 6060, "Learn various healing magic." }, // IDS_SKILL_INFO_CLERIC0
            { 6061, "Learn magic that increase character's stat points." }, // IDS_SKILL_INFO_CLERIC1
            { 6062, "Learn simple attack magic." }, // IDS_SKILL_INFO_CLERIC2
            { 6063, "Learn the Master Skill of a Priest." }, // IDS_SKILL_INFO_CLERIC3
            { 6066, "cannot select the character." }, // IDS_ERR_CHARACTER_SELECT
            { 6067, "Picked up %d Coins." }, // IDS_DROPPED_NOAH_GET
            { 6068, "The durability on %s is all gone." }, // IDS_DURABILITY_EXOAST
            { 6069, "Picked up %s" }, // IDS_ITEM_GET_BY_RULE
            { 6070, "%s refused the request for a trade." }, // IDS_OTHER_PER_TRADE_NO
            { 6071, "Failed trading." }, // IDS_PER_TRADE_FAIL
            { 6072, "Trading with %s has been canceled." }, // IDS_OTHER_PER_TRADE_CANCEL
            { 6073, "Bronze" }, // IDS_ITEM_KIND_BRONS
            { 6074, "Silver" }, // IDS_ITEM_KIND_SILVER
            { 6075, "Gold" }, // IDS_ITEM_KIND_GOLDEN
            { 6076, "Platinum" }, // IDS_ITEM_KIND_PLATINUM
            { 6077, "Crimson" }, // IDS_ITEM_KIND_CRIMSON
            { 6078, "Luna" }, // IDS_ITEM_KIND_LUNA
            { 6079, "Sola" }, // IDS_ITEM_KIND_SOLAR
            { 6080, "Ancient" }, // IDS_ITEM_KIND_ANCIENT
            { 6081, "Mystic" }, // IDS_ITEM_KIND_MISTIQ
            { 6082, "Coin : %d" }, // IDS_TOOLTIP_NOAH
            { 6084, "Repair Cost" }, // IDS_TOOLTIP_REPAIR_PRICE
            { 6085, "irreparable item" }, // IDS_TOOLTIP_CANNOT
            { 6086, "You need %d Coins" }, // IDS_POINTINIT_NOT_ENOUGH_NOAH
            { 6087, "There are no points to reset." }, // IDS_POINTINIT_ALREADY
            { 6088, "Earned %d Coins." }, // IDS_NOAH_CHANGE_GET
            { 6089, "Lost %d Coins." }, // IDS_NOAH_CHANGE_LOST
            { 6099, "Used %d Coins." }, // IDS_NOAH_CHANGE_SPEND
            { 6100, "Earned %d national points." }, // IDS_LOYALTY_CHANGE_GET
            { 6101, "Earned %d manner points." }, // IDS_MANNER_CHANGE_GET
            { 6102, "Congratulations on reaching level 30 on Beginner Helper event." }, // IDS_BEGINNER_HELPER_30
            { 6103, "Received %d leader points." }, // IDS_LADDER_CHANGE_GET
            { 6104, "%d 국가기여도를 잃었습니다." }, // IDS_LOYALTY_CHANGE_LOST
            { 6105, "%d 레더포인트를 잃었습니다." }, // IDS_LADDER_CHANGE_LOST
            { 6112, "You cannot change your stat while there are items equipped on you." }, // IDS_MSG_HASITEMINSLOT
            { 6116, "You are too far away from the NPC." }, // IDS_ERR_REQUEST_NPC_EVENT_SO_FAR
            { 6120, "Invalid password." }, // IDS_WRONG_PASSWORD
            { 6123, "Seeking Party : Level %d ~ %d" }, // IDS_WANT_PARTY_MEMBER
            { 6124, "\n\nKarus Character Window." }, // IDS_SETTING_KARUS_SCREEN
            { 6125, "\n\nEl Morad Character Window." }, // IDS_SETTING_ELMORAD_SCREEN
            { 6300, "Posting request for a party on the Party Request Board." }, // IDS_PARTY_BBS_REGISTER
            { 6301, "Delete the request for a party from the Party Request Board." }, // IDS_PARTY_BBS_REGISTER_CANCEL
            { 6302, "You need %d Coins to register the selling item." }, // IDS_TRADE_BBS_SELL_REGISTER
            { 6303, "You'll have to pay the Inn Keeper %d Coins in order to trade with somebody that is far away." }, // IDS_TRADE_BBS_PER_TRADE
            { 6304, "Registering items you want to buy will cost %d coins per hour." }, // IDS_TRADE_BBS_BUY_REGISTER
            { 6305, "Failed registering." }, // IDS_TRADE_BBS_FAIL1
            { 6306, "You don't have enough Coins." }, // IDS_TRADE_BBS_FAIL2
            { 6307, "Failed canceling the registration." }, // IDS_TRADE_BBS_FAIL3
            { 6308, "Failed. Please press the refresh button." }, // IDS_TRADE_BBS_FAIL4
            { 6309, "Failed requesting for a trade." }, // IDS_TRADE_BBS_FAIL5
            { 6310, "Could not access the trade board." }, // IDS_TRADE_BBS_FAIL6
            { 6311, "The user has declined the request for a trade." }, // IDS_OTHER_PER_TRADE_ID_NO
            { 6500, "Sorry.  A weakling like you are not fit to become a leader!!" }, // IDS_CLAN_DENY_LOWLEVEL
            { 6501, "Sorry.  You need %d Coins in order to create a clan." }, // IDS_CLAN_DENY_LOWGOLD
            { 6502, "You cannot create a clan today." }, // IDS_CLAN_DENY_INVALIDDAY
            { 6503, "You can't create a clan because you're already in another clan." }, // IDS_CLAN_DENY_ALREADYJOINED
            { 6504, "Hm.. You can't create a can right now.  Please come back later." }, // IDS_CLAN_DENY_UNKNOWN
            { 6505, "You are now a leader of a clan.  Congratulations!!!" }, // IDS_CLAN_MAKE_SUCCESS
            { 6506, "You need %d Coins to create a clan." }, // IDS_CLAN_WARNING_COST
            { 6507, "Oh~ho~.. Welcome my brave friend.. Tell me, what would you like to name your clan?" }, // IDS_CLAN_INPUT_NAME
            { 6508, "Oh~ I'm sorry, but somebody else is already using that name. Try a different name." }, // IDS_CLAN_REINPUT_NAME
            { 6509, "Successfully quitted the clan." }, // IDS_CLAN_WITHDRAW_SUCCESS
            { 6510, "Failed quitting the clan." }, // IDS_CLAN_WITHDRAW_FAIL
            { 6511, "Successfully joined the clan." }, // IDS_CLAN_JOIN_SUCCESS
            { 6512, "Failed because the clan has reached the maximum number of people allowed." }, // IDS_CLAN_JOIN_FAIL_CLAN_FULL
            { 6513, "This clan is not valid." }, // IDS_CLAN_JOIN_FAIL_NONE_CLAN
            { 6514, "You do not have the authority." }, // IDS_CLAN_JOIN_FAIL_INVALIDRIGHT
            { 6515, "This user is already in a clan." }, // IDS_CLAN_JOIN_FAIL_OTHER_CLAN_USER
            { 6516, "This user is from a different nation." }, // IDS_CLAN_JOIN_FAIL_ENEMY_USER
            { 6517, "This user is dead." }, // IDS_CLAN_JOIN_FAIL_DEAD_USER
            { 6518, "This user does not exist." }, // IDS_CLAN_JOIN_FAIL_NONE_USER
            { 6519, "You cannot choose yourself." }, // IDS_CLAN_COMMON_FAIL_ME
            { 6520, "This user is not in the clan." }, // IDS_CLAN_COMMON_FAIL_NOTJOINED
            { 6521, "Will you join the clan %s ?" }, // IDS_CLAN_JOIN_REQ
            { 6522, "The user has declined." }, // IDS_CLAN_JOIN_REJECT
            { 6523, "It is not allowed in this zone." }, // IDS_CLAN_COMMON_FAIL_BATTLEZONE
            { 6524, "Exiting game and configuring options. Will you continue?" }, // IDS_CONFIRM_EXECUTE_OPTION
            { 6525, "Saved character information." }, // IDS_REQUEST_GAME_SAVE
            { 6526, "You can save every %d minutes." }, // IDS_DELAY_GAME_SAVE
            { 6527, "Creating a clan is only allowed in the 1st server group." }, // IDS_CLAN_DENY_INVALID_SERVER
            { 6600, "Return to town." }, // IDS_DEAD_RETURN_TOWN
            { 6601, "Re-spawn." }, // IDS_DEAD_REVIVAL
            { 6602, "You don't have enough Stone of Life." }, // IDS_DEAD_LACK_LIFE_STONE
            { 6603, "You need to offer %d Stone of Life on to the shrine if you want to be revived." }, // IDS_DEAD_REVIVAL_MESSAGE
            { 6604, "You cannot be re-spawned because your level is too low." }, // IDS_DEAD_LOW_LEVEL
            { 6605, "Weight :" }, // IDS_INVEN_WEIGHT
            { 6606, "You've arrived at %s." }, // IDS_WARP_ARRIVED_AT
            { 6609, "Connecting.  Please wait." }, // IDS_CONNECTING_PLEASE_WAIT
            { 6610, "You need to be at least level %d." }, // IDS_WARP_MIN_LEVEL
            { 6611, "You cannot enter during the Lunar War." }, // IDS_WARP_NOT_DURING_WAR
            { 6612, "You cannot enter during the Castle Siege War." }, // IDS_WARP_NOT_DURING_CSW
            { 6613, "You cannot enter when you have 0 national points." }, // IDS_WARP_NEED_LOYALTY
            { 6700, "Item upgrade succeeded." }, // IDS_ITEM_UPGRADE_SUCCEEDED
            { 6701, "Item upgrade failed." }, // IDS_ITEM_UPGRADE_FAILED
            { 6702, "Cannot perform item upgrade." }, // IDS_ITEM_UPGRADE_CANNOT_PERFORM
            { 6703, "You don't have enough Coins." }, // IDS_ITEM_UPGRADE_NEED_COINS
            { 6704, "The items required for upgrade does not match." }, // IDS_ITEM_UPGRADE_NO_MATCH
            { 6705, "The item might be destroyed while performing the upgrade.  Will you continue?" }, // IDS_ITEM_UPGRADE_CONFIRM
            { 7612, "To teleport to %s, you need %d coins" }, // IDS_TELEPORT_TO_X_NEED_Y_COINS
            { 7632, "Exiting game in %d seconds." }, // IDS_EXITING_GAME_IN_X_SECONDS
            { 7633, "Exiting game canceled." }, // IDS_EXITING_GAME_CANCELED
            { 7634, "You cannot exit from the client during a battle." }, // IDS_CANNOT_EXIT_DURING_A_BATTLE
            { 7657, "Only characters with level 30~50 can enter." }, // IDS_WARP_LEVEL_30_TO_50
            { 7658, "Please equip your weapon." }, // IDS_SKILL_FAIL_PLEASE_EQUIP_YOUR_WEAPON
            { 7659, "You cannot enter because you do not qualify." }, // IDS_WARP_DO_NOT_QUALIFY
            { 7800, "Private" }, // IDS_PRIVATE_CMD_CAT
            { 7801, "Trade" }, // IDS_TRADE_CMD_CAT
            { 7802, "Party" }, // IDS_PARTY_CMD_CAT
            { 7803, "Clan" }, // IDS_CLAN_CMD_CAT
            { 7804, "Knights" }, // IDS_KNIGHTS_CMD_CAT
            { 7805, "Guardian Monster" }, // IDS_GUARDIAN_MON_CMD_CAT
            { 7806, "King" }, // IDS_KING_CMD_CAT
            { 7807, "GM" }, // IDS_GM_CMD_CAT
            { 7900, "Character related commands." }, // IDS_PRIVATE_TIP
            { 7901, "Trade related commands." }, // IDS_TRADE_TIP
            { 7902, "Party related commands." }, // IDS_PARTY_TIP
            { 7903, "Clan related commands." }, // IDS_CLAN_TIP
            { 7904, "Knights related commands." }, // IDS_KNIGHTS_TIP
            { 7905, "Commands for guard monsters." }, // IDS_GUARDIAN_MON_TIP
            { 7906, "Commands for King." }, // IDS_KING_TIP
            { 8000, "PM" }, // IDS_PRIVATE_PM_CMD
            { 8001, "Town" }, // IDS_PRIVATE_TWN_CMD
            { 8002, "Exit" }, // IDS_PRIVATE_EXIT_CMD
            { 8003, "Greeting" }, // IDS_PRIVATE_GREET_CMD
            { 8004, "Greeting2" }, // IDS_PRIVATE_GREET2_CMD
            { 8005, "Greeting3" }, // IDS_PRIVATE_GREET3_CMD
            { 8006, "Provoke" }, // IDS_PRIVATE_PROVOKE_CMD
            { 8007, "Provoke2" }, // IDS_PRIVATE_PROVOKE2_CMD
            { 8008, "Provoke3" }, // IDS_PRIVATE_PROVOKE3_CMD
            { 8009, "Save" }, // IDS_PRIVATE_SAVE_CMD
            { 8010, "Recommend" }, // IDS_PRIVATE_BATTLE_CMD
            { 7613, "You've received the %s item." }, // IDS_ITEM_RECEIVED
            { 7682, "Received %s coins." }, // IDS_TRADE_COIN_RECV
            { 7683, "Paid %s coins." }, // IDS_TRADE_COIN_PAID
            { 8011, "Individual_Battle" }, // IDS_CMD_INDIVIDUAL_BATTLE
            { 8012, "앉기/서기" }, // IDS_CMD_SIT_STAND
            { 8013, "걷기/뛰기" }, // IDS_CMD_WALK_RUN
            { 8014, "위치" }, // IDS_CMD_LOCATION
            { 8200, "Trade" }, // IDS_CMD_TRADE
            { 8201, "Block_Trade_Request" }, // IDS_CMD_FORBIDTRADE
            { 8202, "Allow_Trade_Request" }, // IDS_CMD_PERMITTRADE
            { 8203, "Merchant" }, // IDS_CMD_MERCHANT
            { 8400, "Party" }, // IDS_CMD_PARTY
            { 8401, "Quit_party" }, // IDS_CMD_LEAVEPARTY
            { 8402, "Seeking_Party" }, // IDS_CMD_RECRUITPARTY
            { 8403, "Block_Party" }, // IDS_CMD_FORBIDPARTY
            { 8404, "Allow_Party" }, // IDS_CMD_PERMITPARTY
            { 8600, "Join_Clan" }, // IDS_CMD_JOINCLAN
            { 8601, "Quit_Clan" }, // IDS_CMD_WITHDRAWCLAN
            { 8602, "Ban_from_Clan" }, // IDS_CMD_FIRECLAN
            { 8603, "Command" }, // IDS_CMD_COMMAND
            { 8604, "Clan_War" }, // IDS_CMD_CLAN_WAR
            { 8605, "Surrender" }, // IDS_CMD_SURRENDER
            { 8606, "Appoint_as_Assistant_Clan_Leader" }, // IDS_CMD_APPOINTVICECHIEF
            { 8607, "Clan_Chat" }, // IDS_CMD_CLAN_CHAT
            { 8608, "Clan_Battle" }, // IDS_CMD_CLAN_BATTLE
            { 8800, "Confederacy" }, // IDS_CMD_CONFEDERACY
            { 8801, "Ban_Knights" }, // IDS_CMD_BAN_KNIGHTS
            { 8802, "Quit_Knights" }, // IDS_CMD_QUIT_KNIGHTS
            { 8803, "Base" }, // IDS_CMD_BASE
            { 8804, "Declaration" }, // IDS_CMD_DECLARATION
            { 9000, "VISIBLE" }, // IDS_CMD_VISIBLE
            { 9001, "INVISIBLE" }, // IDS_CMD_INVISIBLE
            { 9002, "CLEAN" }, // IDS_CMD_CLEAN
            { 9003, "RAINING" }, // IDS_CMD_RAINING
            { 9004, "SNOWING" }, // IDS_CMD_SNOWING
            { 9005, "TIME" }, // IDS_CMD_TIME
            { 9006, "COUNT" }, // IDS_CMD_CU_COUNT
            { 9007, "NOTICE" }, // IDS_CMD_NOTICE
            { 9008, "ARREST" }, // IDS_CMD_ARREST
            { 9009, "FORBIDCONNECT" }, // IDS_CMD_FORBIDCONNECT
            { 9010, "FORBIDCHAT" }, // IDS_CMD_FORBIDCHAT
            { 9011, "PERMITCHAT" }, // IDS_CMD_PERMITCHAT
            { 9012, "NOTICEALL" }, // IDS_CMD_NOTICEALL
            { 9013, "CUTOFF" }, // IDS_CMD_CUTOFF
            { 9014, "VIEW" }, // IDS_CMD_VIEW
            { 9015, "UNVIEW" }, // IDS_CMD_UNVIEW
            { 9016, "FORBIDUSER" }, // IDS_CMD_FORBIDUSER
            { 9017, "SUMMONUSER" }, // IDS_CMD_SUMMONUSER
            { 9018, "ATTACKDISABLE" }, // IDS_CMD_ATTACKDISABLE
            { 9019, "ATTAKCENABLE" }, // IDS_CMD_ATTACKENABLE
            { 9020, "PROGRAMLISTCHECK" }, // IDS_CMD_PLC
            { 9200, "Hide" }, // IDS_CMD_HIDE
            { 9201, "Guard" }, // IDS_CMD_GUARD
            { 9202, "Defend" }, // IDS_CMD_DEFEND
            { 9203, "Look Out" }, // IDS_CMD_LOOK_OUT
            { 9204, "Strategic Formation" }, // IDS_CMD_STRATEGIC_FORMATION
            { 9205, "Rest" }, // IDS_CMD_REST
            { 9206, "Destroy" }, // IDS_CMD_DESTROY
            { 9400, "RoyalOrder" }, // IDS_CMD_ROYALORDER
            { 9401, "Prize" }, // IDS_CMD_PRIZE
            { 9402, "ExperiencePoint" }, // IDS_CMD_EXPERIENCEPOINT
            { 9403, "DropRate" }, // IDS_CMD_DROPRATE
            { 9404, "Rain" }, // IDS_CMD_RAIN
            { 9405, "Snow" }, // IDS_CMD_SNOW
            { 9406, "Clear" }, // IDS_CMD_CLEAR
            { 9407, "Reward" }, // IDS_CMD_REWARD
            { 10100, "You can only search up to +%d levels from your current level." }, // IDS_QUEST_SEARCH_LEVEL_ERROR
            { 11500, "Will you sell %s?" }, // IDS_TRANSACTION_OK_CANCEL_MESSAGE
        };

        private static Dictionary<uint, string> _textsTbl;

        public static void Initialize(string filePath = "Texts_us.tbl")
        {
            if (_textsTbl != null) return;
            try
            {
                _textsTbl = EntropyOnline.Import.KOTableReader.LoadTextsTable(filePath);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[StringTableService] Failed to load Texts_us.tbl: {ex.Message}");
            }
        }

        /// <summary>
        /// String ID'ye göre metin döndürür.
        /// Arama sırası (Open-KO birebir):
        ///   1. Texts_us.tbl (dinamik metinler) — ÖNCELİKLİ!
        ///   2. Hardcoded _table (sistem/local fallback)
        ///   3. Quest_Talk_us.tbl — UIQuestTalk.cpp:50-53, UIQuestMenu.cpp:171-175
        ///   4. Quest_Menu_us.tbl — UIQuestMenu.cpp:186-188
        /// Bulunamazsa "[#ID]" formatında debug string döner.
        /// </summary>
        public static string Get(int stringId)
        {
            // 1. Texts_us.tbl (dinamik metinler) - FIRST priority!
            if (_textsTbl == null)
            {
                Initialize();
            }

            if (_textsTbl != null && _textsTbl.TryGetValue((uint)stringId, out string tblText))
                return tblText;

            // 2. Hardcoded tablo (sistem/local fallback) - SECOND priority!
            if (_table.TryGetValue(stringId, out string text))
                return text;

            // 3. Quest_Talk_us.tbl — diyalog metinleri
            string questTalk = KOImport.QuestTableParser.FindTalk(stringId);
            if (questTalk != null)
                return questTalk;

            // 4. Quest_Menu_us.tbl — menü buton metinleri
            string questMenu = KOImport.QuestTableParser.FindMenu(stringId);
            if (questMenu != null)
                return questMenu;

            return stringId > 0 ? $"[#{stringId}]" : "";
        }

        /// <summary>
        /// String ID mevcut mu kontrol eder.
        /// </summary>
        public static bool Has(int stringId)
        {
            if (_table.ContainsKey(stringId)) return true;
            if (_textsTbl == null) Initialize();
            return _textsTbl != null && _textsTbl.ContainsKey((uint)stringId);
        }

        /// <summary>
        /// Toplam string sayısını döndürür.
        /// </summary>
        public static int Count
        {
            get
            {
                if (_textsTbl == null) Initialize();
                return _table.Count + (_textsTbl?.Count ?? 0);
            }
        }
    }
}
