using System;
using System.Collections.Generic;
using EntropyOnline.UI;
using KOImport;
using UnityEngine;
using UnityEngine.UI;

public class KOMobileAutoAttackSettingsUI : MonoBehaviour
{
	private Font _uiFont;

	private Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

	private Image[] _attackSlots = new Image[12];

	private int[] _attackSkillIds = new int[12];

	private Image[] _buffSlots = new Image[12];

	private int[] _buffSkillIds = new int[12];

	private int[] _buffItemIds = new int[12];

	private Image _hpPotSlot;

	private int _hpPotItemId;

	private Image _mpPotSlot;

	private int _mpPotItemId;

	public float AttackRange = 40f;

	public float RecoveryThreshold = 80f;

	public float HpPotThreshold = 80f;

	public float MpPotThreshold = 80f;

	public bool BasicAttackEnabled = true;

	public bool AutoGenieEnabled = true;

	public bool FollowLeaderEnabled = false;

	public bool SkillInOrderEnabled = false;

	public static KOMobileAutoAttackSettingsUI Instance { get; private set; }

	public int[] AttackSkillIds => _attackSkillIds;

	public int[] BuffSkillIds => _buffSkillIds;

	public int[] BuffItemIds => _buffItemIds;

	public int HpPotItemId => _hpPotItemId;

	public int MpPotItemId => _mpPotItemId;

	private void Awake()
	{
		Instance = this;
		RectTransform rectTransform = GetComponent<RectTransform>();
		if (rectTransform == null)
		{
			rectTransform = base.gameObject.AddComponent<RectTransform>();
		}
		rectTransform.anchorMin = new Vector2(0f, 0.5f);
		rectTransform.anchorMax = new Vector2(0f, 0.5f);
		rectTransform.pivot = new Vector2(0f, 0.5f);
		rectTransform.anchoredPosition = new Vector2(50f, 0f);
		rectTransform.sizeDelta = new Vector2(320f, 0f);
		ContentSizeFitter contentSizeFitter = base.gameObject.AddComponent<ContentSizeFitter>();
		contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		KOUIPanelSlideIn kOUIPanelSlideIn = base.gameObject.AddComponent<KOUIPanelSlideIn>();
		kOUIPanelSlideIn.IsLeft = true;
		kOUIPanelSlideIn.TargetX = 50f;
		kOUIPanelSlideIn.StartX = -350f;
		kOUIPanelSlideIn.Duration = 0.2f;
	}

	private void Start()
	{
		_uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
		if (_uiFont == null)
		{
			_uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
		}
		CreatePanelLayout();
	}

	private void CreatePanelLayout()
	{
		Image ımage = base.gameObject.AddComponent<Image>();
		ımage.sprite = GetPanelBgSprite("settings_bg_gradient", 320, 520, 0, new Color(0.12f, 0.1f, 0.08f, 0.98f), new Color(0.04f, 0.04f, 0.04f, 0.98f), new Color(0.6f, 0.48f, 0.22f, 0.9f), 2);
		VerticalLayoutGroup verticalLayoutGroup = base.gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.spacing = 6f;
		verticalLayoutGroup.padding = new RectOffset(12, 12, 12, 18);
		verticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = false;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		GameObject gameObject = CreateSubContainer("HeaderRow", 30f);
		GameObject gameObject2 = new GameObject("Title", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0f, 0f);
		component.anchorMax = new Vector2(0.8f, 1f);
		component.offsetMin = new Vector2(8f, 0f);
		component.offsetMax = Vector2.zero;
		Text text = gameObject2.AddComponent<Text>();
		text.font = _uiFont;
		text.fontSize = 13;
		text.fontStyle = FontStyle.Bold;
		text.alignment = TextAnchor.MiddleLeft;
		text.color = new Color(0.9f, 0.75f, 0.25f);
		text.text = "AUTO ATTACK SETTINGS";
		AddTextShadow(gameObject2);
		GameObject gameObject3 = new GameObject("CloseBtn", typeof(RectTransform));
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject3.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(1f, 0.5f);
		component2.anchorMax = new Vector2(1f, 0.5f);
		component2.pivot = new Vector2(1f, 0.5f);
		component2.anchoredPosition = new Vector2(-8f, 0f);
		component2.sizeDelta = new Vector2(24f, 24f);
		Image ımage2 = gameObject3.AddComponent<Image>();
		ımage2.sprite = GetRoundedRectSprite("close_btn_square", 24, 24, 0, new Color(0.18f, 0.18f, 0.18f, 1f), new Color(0.45f, 0.35f, 0.15f, 1f), 1);
		GameObject gameObject4 = new GameObject("Text", typeof(RectTransform));
		gameObject4.transform.SetParent(gameObject3.transform, worldPositionStays: false);
		StretchUI(gameObject4);
		Text text2 = gameObject4.AddComponent<Text>();
		text2.font = _uiFont;
		text2.fontSize = 14;
		text2.fontStyle = FontStyle.Bold;
		text2.alignment = TextAnchor.MiddleCenter;
		text2.color = new Color(0.85f, 0.85f, 0.7f);
		text2.text = "X";
		AddTextShadow(gameObject4);
		Button button = gameObject3.AddComponent<Button>();
		button.onClick.AddListener(delegate
		{
			KOUIManager.Instance?.ShowAutoAttackSettings(false);
			KOUIManager.Instance?.ShowSkillTree(false);
		});
		GameObject gameObject5 = new GameObject("HeaderDivider", typeof(RectTransform));
		gameObject5.transform.SetParent(base.gameObject.transform, worldPositionStays: false);
		RectTransform component3 = gameObject5.GetComponent<RectTransform>();
		component3.sizeDelta = new Vector2(300f, 2f);
		LayoutElement layoutElement = gameObject5.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = 2f;
		layoutElement.minHeight = 2f;
		GameObject gameObject6 = new GameObject("Line", typeof(RectTransform));
		gameObject6.transform.SetParent(gameObject5.transform, worldPositionStays: false);
		RectTransform component4 = gameObject6.GetComponent<RectTransform>();
		component4.anchorMin = Vector2.zero;
		component4.anchorMax = Vector2.one;
		component4.offsetMin = new Vector2(8f, 0f);
		component4.offsetMax = new Vector2(-8f, 0f);
		Image ımage3 = gameObject6.AddComponent<Image>();
		ımage3.sprite = GetFadingDividerSprite("header_divider_fading", 280, 2, new Color(0.9f, 0.75f, 0.25f, 0.8f));
		CreateSectionHeader("ATTACK & DEBUFF");
		GameObject gameObject7 = CreateGridContainer("AttackGrid", 88f);
		for (int num = 0; num < 12; num++)
		{
			_attackSlots[num] = CreateSlot(gameObject7.transform, num + 1, num);
		}
		CreateSectionHeader("HEAL & BUFFS (HP-AC)");
		GameObject gameObject8 = CreateGridContainer("BuffGrid", 88f);
		for (int num2 = 0; num2 < 12; num2++)
		{
			_buffSlots[num2] = CreateSlot(gameObject8.transform, num2 + 1, num2 + 12);
		}
		GameObject gameObject9 = CreateSubContainer("PotionRow", 34f);
		HorizontalLayoutGroup horizontalLayoutGroup = gameObject9.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.spacing = 10f;
		horizontalLayoutGroup.padding = new RectOffset(8, 8, 0, 0);
		horizontalLayoutGroup.childControlWidth = false;
		horizontalLayoutGroup.childControlHeight = false;
		horizontalLayoutGroup.childForceExpandWidth = false;
		horizontalLayoutGroup.childForceExpandHeight = false;
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
		GameObject gameObject10 = new GameObject("PotionLabel", typeof(RectTransform));
		gameObject10.transform.SetParent(gameObject9.transform, worldPositionStays: false);
		RectTransform component5 = gameObject10.GetComponent<RectTransform>();
		component5.sizeDelta = new Vector2(60f, 30f);
		Text text3 = gameObject10.AddComponent<Text>();
		text3.font = _uiFont;
		text3.fontSize = 11;
		text3.fontStyle = FontStyle.Bold;
		text3.alignment = TextAnchor.MiddleLeft;
		text3.color = new Color(0.85f, 0.75f, 0.55f);
		text3.text = "POTION >";
		AddTextShadow(gameObject10);
		GameObject gameObject11 = new GameObject("HpPotSlot", typeof(RectTransform));
		gameObject11.transform.SetParent(gameObject9.transform, worldPositionStays: false);
		RectTransform component6 = gameObject11.GetComponent<RectTransform>();
		component6.sizeDelta = new Vector2(40f, 40f);
		_hpPotSlot = gameObject11.AddComponent<Image>();
		_hpPotSlot.sprite = GetRoundedRectSprite("slot_hp", 40, 40, 0, new Color(0.08f, 0.08f, 0.08f, 1f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);
		CreateSlotBadge(gameObject11.transform, "HP", new Color(0.7f, 0.1f, 0.1f));
		Button button2 = gameObject11.AddComponent<Button>();
		button2.onClick.AddListener(delegate
		{
			_hpPotSlot.sprite = GetRoundedRectSprite("slot_hp", 40, 40, 0, new Color(0.08f, 0.08f, 0.08f, 1f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);
			_hpPotItemId = 0;
		});
		GameObject gameObject12 = new GameObject("MpPotSlot", typeof(RectTransform));
		gameObject12.transform.SetParent(gameObject9.transform, worldPositionStays: false);
		RectTransform component7 = gameObject12.GetComponent<RectTransform>();
		component7.sizeDelta = new Vector2(40f, 40f);
		_mpPotSlot = gameObject12.AddComponent<Image>();
		_mpPotSlot.sprite = GetRoundedRectSprite("slot_mp", 40, 40, 0, new Color(0.08f, 0.08f, 0.08f, 1f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);
		CreateSlotBadge(gameObject12.transform, "MP", new Color(0.1f, 0.4f, 0.7f));
		Button button3 = gameObject12.AddComponent<Button>();
		button3.onClick.AddListener(delegate
		{
			_mpPotSlot.sprite = GetRoundedRectSprite("slot_mp", 40, 40, 0, new Color(0.08f, 0.08f, 0.08f, 1f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);
			_mpPotItemId = 0;
		});
		CreateSlider(base.transform, "ATTACK RANGE", 0f, 40f, AttackRange, isPercent: false, "m", delegate(float val)
		{
			AttackRange = val;
		});
		CreateSlider(base.transform, "USE SKILL RECOVERY AT", 0f, 100f, RecoveryThreshold, isPercent: true, "%", delegate(float val)
		{
			RecoveryThreshold = val;
		});
		CreateSlider(base.transform, "AT % CONSUME HP POTION", 0f, 100f, HpPotThreshold, isPercent: true, "%", delegate(float val)
		{
			HpPotThreshold = val;
		});
		CreateSlider(base.transform, "AT % CONSUME MP POTION", 0f, 100f, MpPotThreshold, isPercent: true, "%", delegate(float val)
		{
			MpPotThreshold = val;
		});
		GameObject gameObject13 = CreateSubContainer("CheckRow1", 20f);
		HorizontalLayoutGroup horizontalLayoutGroup2 = gameObject13.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup2.spacing = 10f;
		horizontalLayoutGroup2.padding = new RectOffset(8, 8, 0, 0);
		horizontalLayoutGroup2.childControlWidth = true;
		horizontalLayoutGroup2.childForceExpandWidth = true;
		CreateCheckbox(gameObject13.transform, "[R] Basic Attack", BasicAttackEnabled, delegate(bool val)
		{
			BasicAttackEnabled = val;
		});
		CreateCheckbox(gameObject13.transform, "Auto Genie Sprit", AutoGenieEnabled, delegate(bool val)
		{
			AutoGenieEnabled = val;
		});
		GameObject gameObject14 = CreateSubContainer("CheckRow2", 20f);
		HorizontalLayoutGroup horizontalLayoutGroup3 = gameObject14.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup3.spacing = 10f;
		horizontalLayoutGroup3.padding = new RectOffset(8, 8, 0, 0);
		horizontalLayoutGroup3.childControlWidth = true;
		horizontalLayoutGroup3.childForceExpandWidth = true;
		CreateCheckbox(gameObject14.transform, "Follow Party Leader", FollowLeaderEnabled, delegate(bool val)
		{
			FollowLeaderEnabled = val;
		});
		CreateCheckbox(gameObject14.transform, "Skill Use in Order", SkillInOrderEnabled, delegate(bool val)
		{
			SkillInOrderEnabled = val;
		});
	}

	public int GetSlotAtScreenPosition(Vector2 screenPos)
	{
		for (int i = 0; i < 12; i++)
		{
			if (!(_attackSlots[i] == null) && IsPointInsideRectTransform(_attackSlots[i].rectTransform, screenPos))
			{
				return i;
			}
		}
		for (int j = 0; j < 12; j++)
		{
			if (!(_buffSlots[j] == null) && IsPointInsideRectTransform(_buffSlots[j].rectTransform, screenPos))
			{
				return j + 12;
			}
		}
		return -1;
	}

	public int GetItemSlotAtScreenPosition(Vector2 screenPos)
	{
		for (int i = 0; i < 12; i++)
		{
			if (!(_buffSlots[i] == null) && IsPointInsideRectTransform(_buffSlots[i].rectTransform, screenPos))
			{
				return i + 12;
			}
		}
		if (_hpPotSlot != null && IsPointInsideRectTransform(_hpPotSlot.rectTransform, screenPos))
		{
			return 24;
		}
		if (_mpPotSlot != null && IsPointInsideRectTransform(_mpPotSlot.rectTransform, screenPos))
		{
			return 25;
		}
		return -1;
	}

	private bool IsPointInsideRectTransform(RectTransform rt, Vector2 screenPos)
	{
		if (rt == null)
		{
			return false;
		}
		Vector3[] array = new Vector3[4];
		rt.GetWorldCorners(array);
		float num = Mathf.Min(array[0].x, array[2].x);
		float num2 = Mathf.Max(array[0].x, array[2].x);
		float num3 = Mathf.Min(array[0].y, array[2].y);
		float num4 = Mathf.Max(array[0].y, array[2].y);
		num -= 10f;
		num2 += 10f;
		num3 -= 10f;
		num4 += 10f;
		return screenPos.x >= num && screenPos.x <= num2 && screenPos.y >= num3 && screenPos.y <= num4;
	}

	public void SetSkillIcon(int slotIndex, Sprite icon, int magicNum)
	{
		if (slotIndex >= 0 && slotIndex < 12)
		{
			_attackSlots[slotIndex].sprite = icon;
			_attackSlots[slotIndex].color = Color.white;
			_attackSkillIds[slotIndex] = magicNum;
		}
		else if (slotIndex >= 12 && slotIndex < 24)
		{
			int num = slotIndex - 12;
			_buffSlots[num].sprite = icon;
			_buffSlots[num].color = Color.white;
			_buffSkillIds[num] = magicNum;
			_buffItemIds[num] = 0;
		}
	}

	public void SetItemSlot(int slotIndex, KOItemSlotHandler itemSlot)
	{
		if (itemSlot == null)
		{
			return;
		}
		Sprite sprite = itemSlot.GetComponent<Image>()?.sprite;
		if (!(sprite == null))
		{
			int num = itemSlot.tooltipItemDefId;
			if (num <= 0 && itemSlot.itemData != null)
			{
				num = itemSlot.itemData.ItemDefId;
			}
			SetItemSlotRaw(slotIndex, num, sprite);
		}
	}

	public void SetItemSlotRaw(int slotIndex, int itemId, Sprite icon)
	{
		if (!(icon == null))
		{
			if (slotIndex == 24)
			{
				_hpPotSlot.sprite = icon;
				_hpPotSlot.color = Color.white;
				_hpPotItemId = itemId;
			}
			else if (slotIndex == 25)
			{
				_mpPotSlot.sprite = icon;
				_mpPotSlot.color = Color.white;
				_mpPotItemId = itemId;
			}
			else if (slotIndex >= 12 && slotIndex < 24)
			{
				int num = slotIndex - 12;
				_buffSlots[num].sprite = icon;
				_buffSlots[num].color = Color.white;
				_buffItemIds[num] = itemId;
				_buffSkillIds[num] = 0;
			}
		}
	}

	private void CreateSectionHeader(string title)
	{
		GameObject gameObject = CreateSubContainer("Header_" + title, 16f);
		GameObject gameObject2 = new GameObject("Text", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component = gameObject2.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = new Vector2(8f, 0f);
		component.offsetMax = Vector2.zero;
		Text text = gameObject2.AddComponent<Text>();
		text.font = _uiFont;
		text.fontSize = 11;
		text.fontStyle = FontStyle.Bold;
		text.alignment = TextAnchor.MiddleLeft;
		text.color = new Color(0.85f, 0.75f, 0.55f);
		text.text = title;
		AddTextShadow(gameObject2);
	}

	private Image CreateSlot(Transform parent, int number, int slotIndex)
	{
		GameObject gameObject = new GameObject("Slot_" + slotIndex, typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.sizeDelta = new Vector2(40f, 40f);
		Image img = gameObject.AddComponent<Image>();
		img.sprite = GetRoundedRectSprite("slot_bg_" + slotIndex, 40, 40, 0, new Color(0.08f, 0.08f, 0.08f, 1f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);
		Button button = gameObject.AddComponent<Button>();
		button.onClick.AddListener(delegate
		{
			img.sprite = GetRoundedRectSprite("slot_bg_" + slotIndex, 40, 40, 0, new Color(0.08f, 0.08f, 0.08f, 1f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);
			if (slotIndex < 12)
			{
				_attackSkillIds[slotIndex] = 0;
			}
			else if (slotIndex < 24)
			{
				_buffSkillIds[slotIndex - 12] = 0;
				_buffItemIds[slotIndex - 12] = 0;
			}
		});
		GameObject gameObject2 = new GameObject("Badge", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 1f);
		component2.anchorMax = new Vector2(0f, 1f);
		component2.pivot = new Vector2(0f, 1f);
		component2.anchoredPosition = new Vector2(1f, -1f);
		component2.sizeDelta = new Vector2(10f, 10f);
		Text text = gameObject2.AddComponent<Text>();
		text.font = _uiFont;
		text.fontSize = 9;
		text.fontStyle = FontStyle.Bold;
		text.alignment = TextAnchor.MiddleCenter;
		text.color = new Color(0.9f, 0.75f, 0.25f);
		text.text = number.ToString();
		AddTextShadow(gameObject2);
		return img;
	}

	private void CreateSlotBadge(Transform parent, string text, Color badgeColor)
	{
		GameObject gameObject = new GameObject("Badge", typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.anchorMin = new Vector2(0.5f, 1f);
		component.anchorMax = new Vector2(0.5f, 1f);
		component.pivot = new Vector2(0.5f, 0.5f);
		component.anchoredPosition = new Vector2(0f, 0f);
		component.sizeDelta = new Vector2(16f, 9f);
		Image ımage = gameObject.AddComponent<Image>();
		ımage.sprite = GetRoundedRectSprite("badge_bg_" + text, 16, 9, 0, badgeColor, badgeColor, 0);
		GameObject gameObject2 = new GameObject("Text", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		StretchUI(gameObject2);
		Text text2 = gameObject2.AddComponent<Text>();
		text2.font = _uiFont;
		text2.fontSize = 8;
		text2.fontStyle = FontStyle.Bold;
		text2.alignment = TextAnchor.MiddleCenter;
		text2.color = Color.white;
		text2.text = text;
	}

	private Slider CreateSlider(Transform parent, string labelText, float minVal, float maxVal, float startVal, bool isPercent, string suffix, Action<float> onValChanged)
	{
		GameObject gameObject = new GameObject("Slider_" + labelText, typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.sizeDelta = new Vector2(300f, 36f);
		LayoutElement layoutElement = gameObject.AddComponent<LayoutElement>();
		layoutElement.preferredHeight = 36f;
		layoutElement.minHeight = 36f;
		GameObject gameObject2 = new GameObject("Label", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0.55f);
		component2.anchorMax = new Vector2(0.8f, 1f);
		component2.pivot = new Vector2(0f, 0.5f);
		component2.anchoredPosition = Vector2.zero;
		component2.sizeDelta = Vector2.zero;
		component2.offsetMin = new Vector2(8f, 0f);
		Text text = gameObject2.AddComponent<Text>();
		text.font = _uiFont;
		text.fontSize = 11;
		text.fontStyle = FontStyle.Bold;
		text.color = new Color(0.85f, 0.75f, 0.55f);
		text.text = labelText.ToUpper();
		AddTextShadow(gameObject2);
		GameObject gameObject3 = new GameObject("ValText", typeof(RectTransform));
		gameObject3.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component3 = gameObject3.GetComponent<RectTransform>();
		component3.anchorMin = new Vector2(0.8f, 0.55f);
		component3.anchorMax = new Vector2(1f, 1f);
		component3.pivot = new Vector2(1f, 0.5f);
		component3.anchoredPosition = Vector2.zero;
		component3.sizeDelta = Vector2.zero;
		component3.offsetMax = new Vector2(-8f, 0f);
		Text valTxt = gameObject3.AddComponent<Text>();
		valTxt.font = _uiFont;
		valTxt.fontSize = 11;
		valTxt.fontStyle = FontStyle.Bold;
		valTxt.alignment = TextAnchor.MiddleRight;
		valTxt.color = Color.white;
		valTxt.text = (isPercent ? (suffix + startVal) : (startVal + suffix));
		AddTextShadow(gameObject3);
		GameObject gameObject4 = new GameObject("Slider", typeof(RectTransform));
		gameObject4.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component4 = gameObject4.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0f, 0f);
		component4.anchorMax = new Vector2(1f, 0.45f);
		component4.pivot = new Vector2(0.5f, 0.5f);
		component4.anchoredPosition = Vector2.zero;
		component4.sizeDelta = Vector2.zero;
		Slider slider = gameObject4.AddComponent<Slider>();
		Color color = new Color(0.45f, 0.35f, 0.15f, 0.8f);
		Color fillColor = new Color(0.85f, 0.75f, 0.55f, 1f);
		Color borderColor = new Color(0.45f, 0.35f, 0.15f, 1f);
		if (labelText.Contains("HP"))
		{
			color = new Color(0.8f, 0.1f, 0.1f, 1f);
			fillColor = new Color(0.8f, 0.1f, 0.1f, 1f);
			borderColor = new Color(0.4f, 0.05f, 0.05f, 1f);
		}
		else if (labelText.Contains("MP"))
		{
			color = new Color(0.1f, 0.5f, 0.8f, 1f);
			fillColor = new Color(0.1f, 0.5f, 0.8f, 1f);
			borderColor = new Color(0.05f, 0.25f, 0.4f, 1f);
		}
		GameObject gameObject5 = new GameObject("Background", typeof(RectTransform));
		gameObject5.transform.SetParent(gameObject4.transform, worldPositionStays: false);
		RectTransform component5 = gameObject5.GetComponent<RectTransform>();
		component5.anchorMin = new Vector2(0f, 0.4f);
		component5.anchorMax = new Vector2(1f, 0.6f);
		component5.offsetMin = new Vector2(8f, 0f);
		component5.offsetMax = new Vector2(-8f, 0f);
		Image ımage = gameObject5.AddComponent<Image>();
		ımage.sprite = GetRoundedRectSprite("slider_bg_" + labelText, 200, 4, 0, color, color, 0);
		GameObject gameObject6 = new GameObject("Handle Slide Area", typeof(RectTransform));
		gameObject6.transform.SetParent(gameObject4.transform, worldPositionStays: false);
		RectTransform component6 = gameObject6.GetComponent<RectTransform>();
		component6.anchorMin = new Vector2(0f, 0f);
		component6.anchorMax = new Vector2(1f, 1f);
		component6.offsetMin = new Vector2(18f, 0f);
		component6.offsetMax = new Vector2(-18f, 0f);
		GameObject gameObject7 = new GameObject("Handle", typeof(RectTransform));
		gameObject7.transform.SetParent(gameObject6.transform, worldPositionStays: false);
		RectTransform component7 = gameObject7.GetComponent<RectTransform>();
		component7.anchorMin = new Vector2(0f, 0.5f);
		component7.anchorMax = new Vector2(0f, 0.5f);
		component7.pivot = new Vector2(0.5f, 0.5f);
		component7.anchoredPosition = Vector2.zero;
		component7.sizeDelta = new Vector2(28f, 14f);
		GameObject gameObject8 = new GameObject("Visual", typeof(RectTransform));
		gameObject8.transform.SetParent(gameObject7.transform, worldPositionStays: false);
		RectTransform component8 = gameObject8.GetComponent<RectTransform>();
		component8.anchorMin = new Vector2(0.5f, 0.5f);
		component8.anchorMax = new Vector2(0.5f, 0.5f);
		component8.pivot = new Vector2(0.5f, 0.5f);
		component8.anchoredPosition = Vector2.zero;
		component8.sizeDelta = new Vector2(28f, 12f);
		Image ımage2 = gameObject8.AddComponent<Image>();
		ımage2.sprite = GetRoundedRectSprite("slider_handle_visual_" + labelText, 28, 12, 6, fillColor, borderColor, 1);
		GameObject gameObject9 = new GameObject("Stripes", typeof(RectTransform));
		gameObject9.transform.SetParent(gameObject8.transform, worldPositionStays: false);
		StretchUI(gameObject9);
		Text text2 = gameObject9.AddComponent<Text>();
		text2.font = _uiFont;
		text2.fontSize = 8;
		text2.fontStyle = FontStyle.Bold;
		text2.alignment = TextAnchor.MiddleCenter;
		text2.color = new Color(0.2f, 0.15f, 0.05f, 0.8f);
		text2.text = "|||";
		slider.handleRect = component7;
		slider.targetGraphic = ımage2;
		slider.minValue = minVal;
		slider.maxValue = maxVal;
		slider.value = startVal;
		slider.onValueChanged.AddListener(delegate(float val)
		{
			valTxt.text = (isPercent ? (suffix + Mathf.RoundToInt(val)) : (Mathf.RoundToInt(val) + suffix));
			onValChanged?.Invoke(val);
		});
		return slider;
	}

	private Toggle CreateCheckbox(Transform parent, string labelText, bool startVal, Action<bool> onValChanged)
	{
		GameObject gameObject = new GameObject("Checkbox_" + labelText, typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.sizeDelta = new Vector2(140f, 20f);
		Toggle toggle = gameObject.AddComponent<Toggle>();
		GameObject gameObject2 = new GameObject("Background", typeof(RectTransform));
		gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component2 = gameObject2.GetComponent<RectTransform>();
		component2.anchorMin = new Vector2(0f, 0.5f);
		component2.anchorMax = new Vector2(0f, 0.5f);
		component2.pivot = new Vector2(0f, 0.5f);
		component2.anchoredPosition = new Vector2(0f, 0f);
		component2.sizeDelta = new Vector2(16f, 16f);
		Image ımage = gameObject2.AddComponent<Image>();
		ımage.sprite = GetRoundedRectSprite("checkbox_bg_" + labelText, 16, 16, 0, new Color(0.08f, 0.08f, 0.08f, 1f), new Color(0.45f, 0.35f, 0.15f, 0.8f), 1);
		GameObject gameObject3 = new GameObject("Checkmark", typeof(RectTransform));
		gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
		RectTransform component3 = gameObject3.GetComponent<RectTransform>();
		component3.anchorMin = Vector2.zero;
		component3.anchorMax = Vector2.one;
		component3.offsetMin = new Vector2(2f, 2f);
		component3.offsetMax = new Vector2(-2f, -2f);
		Image ımage2 = gameObject3.AddComponent<Image>();
		ımage2.sprite = GetRoundedRectSprite("checkbox_tick_" + labelText, 12, 12, 0, new Color(0.2f, 0.65f, 0.2f, 1f), new Color(0.2f, 0.65f, 0.2f, 1f), 0);
		GameObject gameObject4 = new GameObject("Label", typeof(RectTransform));
		gameObject4.transform.SetParent(gameObject.transform, worldPositionStays: false);
		RectTransform component4 = gameObject4.GetComponent<RectTransform>();
		component4.anchorMin = new Vector2(0f, 0f);
		component4.anchorMax = new Vector2(1f, 1f);
		component4.pivot = new Vector2(0f, 0.5f);
		component4.anchoredPosition = new Vector2(22f, 0f);
		component4.sizeDelta = new Vector2(-22f, 0f);
		Text text = gameObject4.AddComponent<Text>();
		text.font = _uiFont;
		text.fontSize = 12;
		text.fontStyle = FontStyle.Bold;
		text.alignment = TextAnchor.MiddleLeft;
		text.color = new Color(0.85f, 0.85f, 0.7f);
		text.text = labelText;
		AddTextShadow(gameObject4);
		toggle.graphic = ımage2;
		toggle.targetGraphic = ımage;
		toggle.isOn = startVal;
		toggle.onValueChanged.AddListener(delegate(bool val)
		{
			onValChanged?.Invoke(val);
		});
		return toggle;
	}

	private GameObject CreateSubContainer(string name, float height)
	{
		GameObject gameObject = new GameObject(name, typeof(RectTransform));
		gameObject.transform.SetParent(base.transform, worldPositionStays: false);
		RectTransform component = gameObject.GetComponent<RectTransform>();
		component.sizeDelta = new Vector2(300f, height);
		return gameObject;
	}

	private GameObject CreateGridContainer(string name, float height)
	{
		GameObject gameObject = CreateSubContainer(name, height);
		GridLayoutGroup gridLayoutGroup = gameObject.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.cellSize = new Vector2(40f, 40f);
		gridLayoutGroup.spacing = new Vector2(8f, 6f);
		gridLayoutGroup.padding = new RectOffset(8, 8, 2, 2);
		gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
		gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
		gridLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
		gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
		gridLayoutGroup.constraintCount = 6;
		return gameObject;
	}

	private void StretchUI(GameObject obj)
	{
		RectTransform component = obj.GetComponent<RectTransform>();
		component.anchorMin = Vector2.zero;
		component.anchorMax = Vector2.one;
		component.offsetMin = Vector2.zero;
		component.offsetMax = Vector2.zero;
	}

	private void AddTextShadow(GameObject obj)
	{
		Shadow shadow = obj.AddComponent<Shadow>();
		shadow.effectColor = Color.black;
		shadow.effectDistance = new Vector2(1f, -1f);
	}

	private Sprite GetRoundedRectSprite(string key, int w, int h, int radius, Color fillColor, Color borderColor, int borderWidth)
	{
		if (_spriteCache.TryGetValue(key, out var value))
		{
			return value;
		}
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = ((radius > 0) ? FilterMode.Bilinear : FilterMode.Point);
		texture2D.wrapMode = TextureWrapMode.Clamp;
		for (int i = 0; i < w; i++)
		{
			for (int j = 0; j < h; j++)
			{
				if (radius <= 0)
				{
					bool flag = i < borderWidth || i >= w - borderWidth || j < borderWidth || j >= h - borderWidth;
					texture2D.SetPixel(i, j, flag ? borderColor : fillColor);
					continue;
				}
				bool flag2 = true;
				float num = 0f;
				float num2 = 0f;
				if (i < radius && j < radius)
				{
					num = radius - i;
					num2 = radius - j;
					flag2 = num * num + num2 * num2 <= (float)(radius * radius);
				}
				else if (i >= w - radius && j < radius)
				{
					num = i - (w - 1 - radius);
					num2 = radius - j;
					flag2 = num * num + num2 * num2 <= (float)(radius * radius);
				}
				else if (i < radius && j >= h - radius)
				{
					num = radius - i;
					num2 = j - (h - 1 - radius);
					flag2 = num * num + num2 * num2 <= (float)(radius * radius);
				}
				else if (i >= w - radius && j >= h - radius)
				{
					num = i - (w - 1 - radius);
					num2 = j - (h - 1 - radius);
					flag2 = num * num + num2 * num2 <= (float)(radius * radius);
				}
				if (flag2)
				{
					bool flag3 = false;
					if (i < borderWidth || i >= w - borderWidth || j < borderWidth || j >= h - borderWidth)
					{
						flag3 = true;
					}
					else if (i < radius || i >= w - radius || j < radius || j >= h - radius)
					{
						float num3 = Mathf.Sqrt(num * num + num2 * num2);
						if (num3 >= (float)(radius - borderWidth))
						{
							flag3 = true;
						}
					}
					float num4 = 0f;
					if (i < radius && j < radius)
					{
						num4 = Mathf.Sqrt(num * num + num2 * num2) - (float)radius;
					}
					else if (i >= w - radius && j < radius)
					{
						num4 = Mathf.Sqrt(num * num + num2 * num2) - (float)radius;
					}
					else if (i < radius && j >= h - radius)
					{
						num4 = Mathf.Sqrt(num * num + num2 * num2) - (float)radius;
					}
					else if (i >= w - radius && j >= h - radius)
					{
						num4 = Mathf.Sqrt(num * num + num2 * num2) - (float)radius;
					}
					if (num4 > 0f && num4 < 1f)
					{
						Color color = (flag3 ? borderColor : fillColor);
						color.a *= 1f - num4;
						texture2D.SetPixel(i, j, color);
					}
					else
					{
						texture2D.SetPixel(i, j, flag3 ? borderColor : fillColor);
					}
				}
				else
				{
					texture2D.SetPixel(i, j, Color.clear);
				}
			}
		}
		texture2D.Apply();
		Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f));
		_spriteCache[key] = sprite;
		return sprite;
	}

	private Sprite GetPanelBgSprite(string key, int w, int h, int radius, Color topColor, Color bottomColor, Color borderColor, int borderWidth)
	{
		if (_spriteCache.TryGetValue(key, out var value))
		{
			return value;
		}
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Bilinear;
		texture2D.wrapMode = TextureWrapMode.Clamp;
		for (int i = 0; i < w; i++)
		{
			for (int j = 0; j < h; j++)
			{
				float t = (float)j / (float)h;
				Color color = Color.Lerp(bottomColor, topColor, t);
				bool flag = true;
				float num = 0f;
				float num2 = 0f;
				if (i < radius && j < radius)
				{
					num = radius - i;
					num2 = radius - j;
					flag = num * num + num2 * num2 <= (float)(radius * radius);
				}
				else if (i >= w - radius && j < radius)
				{
					num = i - (w - 1 - radius);
					num2 = radius - j;
					flag = num * num + num2 * num2 <= (float)(radius * radius);
				}
				else if (i < radius && j >= h - radius)
				{
					num = radius - i;
					num2 = j - (h - 1 - radius);
					flag = num * num + num2 * num2 <= (float)(radius * radius);
				}
				else if (i >= w - radius && j >= h - radius)
				{
					num = i - (w - 1 - radius);
					num2 = j - (h - 1 - radius);
					flag = num * num + num2 * num2 <= (float)(radius * radius);
				}
				if (flag)
				{
					bool flag2 = false;
					if (i < borderWidth || i >= w - borderWidth || j < borderWidth || j >= h - borderWidth)
					{
						flag2 = true;
					}
					else if (i < radius || i >= w - radius || j < radius || j >= h - radius)
					{
						float num3 = Mathf.Sqrt(num * num + num2 * num2);
						if (num3 >= (float)(radius - borderWidth))
						{
							flag2 = true;
						}
					}
					float num4 = 0f;
					if (i < radius && j < radius)
					{
						num4 = Mathf.Sqrt(num * num + num2 * num2) - (float)radius;
					}
					else if (i >= w - radius && j < radius)
					{
						num4 = Mathf.Sqrt(num * num + num2 * num2) - (float)radius;
					}
					else if (i < radius && j >= h - radius)
					{
						num4 = Mathf.Sqrt(num * num + num2 * num2) - (float)radius;
					}
					else if (i >= w - radius && j >= h - radius)
					{
						num4 = Mathf.Sqrt(num * num + num2 * num2) - (float)radius;
					}
					if (num4 > 0f && num4 < 1f)
					{
						Color color2 = (flag2 ? borderColor : color);
						color2.a *= 1f - num4;
						texture2D.SetPixel(i, j, color2);
					}
					else
					{
						texture2D.SetPixel(i, j, flag2 ? borderColor : color);
					}
				}
				else
				{
					texture2D.SetPixel(i, j, Color.clear);
				}
			}
		}
		texture2D.Apply();
		Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f));
		_spriteCache[key] = sprite;
		return sprite;
	}

	public bool ValidateAndDropSkill(int slotIndex, int magicNum, Sprite icon)
	{
		SkillEntry skillEntry = SkillTableParser.Find(magicNum);
		if (skillEntry == null)
		{
			return false;
		}
		if (slotIndex < 12)
		{
			if (skillEntry.Target != 7 && skillEntry.Target != 10)
			{
				KOUIManager.Instance?.ShowToast("Only active attack or debuff skills can be placed in these slots.");
				return false;
			}
			SetSkillIcon(slotIndex, icon, magicNum);
			return true;
		}
		if (slotIndex < 24)
		{
			if (skillEntry.Target != 1 && skillEntry.Target != 2 && skillEntry.Target != 3 && skillEntry.Target != 4 && skillEntry.Target != 6 && skillEntry.Target != 11 && skillEntry.Target != 25)
			{
				KOUIManager.Instance?.ShowToast("Only supportive, healing, or buff skills can be placed in these slots.");
				return false;
			}
			SetSkillIcon(slotIndex, icon, magicNum);
			return true;
		}
		return false;
	}

	public bool ValidateAndDropItem(int slotIndex, int itemId, Sprite icon)
	{
		ItemBasicEntry ıtemBasic = ItemDataManager.GetItemBasic(itemId);
		if (ıtemBasic == null)
		{
			return false;
		}
		if (slotIndex >= 12 && slotIndex < 24)
		{
			if (ıtemBasic.ByClass != 98 && ıtemBasic.ByClass != byte.MaxValue)
			{
				KOUIManager.Instance?.ShowToast("Only buff scrolls can be placed in these slots.");
				return false;
			}
			SkillEntry skillEntry = SkillTableParser.FindByExhaustItem((uint)itemId);
			if (skillEntry == null || (skillEntry.Target != 1 && skillEntry.Target != 2 && skillEntry.Target != 3 && skillEntry.Target != 4 && skillEntry.Target != 6 && skillEntry.Target != 11 && skillEntry.Target != 25))
			{
				KOUIManager.Instance?.ShowToast("Only buff scrolls can be placed in these slots.");
				return false;
			}
			SetItemSlotRaw(slotIndex, itemId, icon);
			return true;
		}
		switch (slotIndex)
		{
		case 24:
			if (ıtemBasic.ByClass != 97 || (ıtemBasic.DwID / 10000 != 38901 && !ıtemBasic.SzName.ToLower().Contains("hp") && !ıtemBasic.SzName.ToLower().Contains("healing")))
			{
				KOUIManager.Instance?.ShowToast("Only HP recovery potions can be placed in this slot.");
				return false;
			}
			SetItemSlotRaw(slotIndex, itemId, icon);
			return true;
		case 25:
			if (ıtemBasic.ByClass != 97 || (ıtemBasic.DwID / 10000 != 38902 && !ıtemBasic.SzName.ToLower().Contains("mp") && !ıtemBasic.SzName.ToLower().Contains("mana")))
			{
				KOUIManager.Instance?.ShowToast("Only MP recovery potions can be placed in this slot.");
				return false;
			}
			SetItemSlotRaw(slotIndex, itemId, icon);
			return true;
		default:
			return false;
		}
	}

	public void HighlightSlotsForSkill(int magicNum)
	{
		SkillEntry skillEntry = SkillTableParser.Find(magicNum);
		if (skillEntry == null)
		{
			return;
		}
		bool flag = skillEntry.Target == 7 || skillEntry.Target == 10;
		bool flag2 = skillEntry.Target == 1 || skillEntry.Target == 2 || skillEntry.Target == 3 || skillEntry.Target == 4 || skillEntry.Target == 6 || skillEntry.Target == 11 || skillEntry.Target == 25;
		for (int i = 0; i < 12; i++)
		{
			if (_attackSlots[i] != null)
			{
				_attackSlots[i].color = (flag ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.6f));
			}
		}
		for (int j = 0; j < 12; j++)
		{
			if (_buffSlots[j] != null)
			{
				_buffSlots[j].color = (flag2 ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.6f));
			}
		}
		if (_hpPotSlot != null)
		{
			_hpPotSlot.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
		}
		if (_mpPotSlot != null)
		{
			_mpPotSlot.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
		}
	}

	public void HighlightSlotsForItem(int itemId)
	{
		ItemBasicEntry ıtemBasic = ItemDataManager.GetItemBasic(itemId);
		if (ıtemBasic == null)
		{
			return;
		}
		bool flag = ıtemBasic.ByClass == 97 && (ıtemBasic.DwID / 10000 == 38901 || ıtemBasic.SzName.ToLower().Contains("hp") || ıtemBasic.SzName.ToLower().Contains("healing"));
		bool flag2 = ıtemBasic.ByClass == 97 && (ıtemBasic.DwID / 10000 == 38902 || ıtemBasic.SzName.ToLower().Contains("mp") || ıtemBasic.SzName.ToLower().Contains("mana"));
		SkillEntry skillEntry = SkillTableParser.FindByExhaustItem((uint)itemId);
		bool flag3 = (ıtemBasic.ByClass == 98 || ıtemBasic.ByClass == byte.MaxValue) && skillEntry != null && (skillEntry.Target == 1 || skillEntry.Target == 2 || skillEntry.Target == 3 || skillEntry.Target == 4 || skillEntry.Target == 6 || skillEntry.Target == 11 || skillEntry.Target == 25);
		for (int i = 0; i < 12; i++)
		{
			if (_attackSlots[i] != null)
			{
				_attackSlots[i].color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
			}
		}
		for (int j = 0; j < 12; j++)
		{
			if (_buffSlots[j] != null)
			{
				_buffSlots[j].color = (flag3 ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.6f));
			}
		}
		if (_hpPotSlot != null)
		{
			_hpPotSlot.color = (flag ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.6f));
		}
		if (_mpPotSlot != null)
		{
			_mpPotSlot.color = (flag2 ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.6f));
		}
	}

	public void ResetSlotHighlights()
	{
		for (int i = 0; i < 12; i++)
		{
			if (_attackSlots[i] != null)
			{
				_attackSlots[i].color = Color.white;
			}
			if (_buffSlots[i] != null)
			{
				_buffSlots[i].color = Color.white;
			}
		}
		if (_hpPotSlot != null)
		{
			_hpPotSlot.color = Color.white;
		}
		if (_mpPotSlot != null)
		{
			_mpPotSlot.color = Color.white;
		}
	}

	private Sprite GetFadingDividerSprite(string key, int w, int h, Color color)
	{
		if (_spriteCache.TryGetValue(key, out var value))
		{
			return value;
		}
		Texture2D texture2D = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		texture2D.filterMode = FilterMode.Bilinear;
		texture2D.wrapMode = TextureWrapMode.Clamp;
		float num = (float)w / 2f;
		for (int i = 0; i < w; i++)
		{
			for (int j = 0; j < h; j++)
			{
				float num2 = Mathf.Abs((float)i - num) / num;
				float f = Mathf.Clamp01(1f - num2);
				f = Mathf.Pow(f, 1.5f);
				Color color2 = color;
				color2.a *= f;
				texture2D.SetPixel(i, j, color2);
			}
		}
		texture2D.Apply();
		Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f));
		_spriteCache[key] = sprite;
		return sprite;
	}
}
