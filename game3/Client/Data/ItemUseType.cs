namespace Client.Data
{
	/// <summary>伺服器 use_type 數值，與 ItemTable._useTypes / etc_items.use_type 對應。客戶端依此判斷物品使用行為，不依 ItemId。</summary>
	public static class ItemUseType
	{
		public const int Normal = 0;
		public const int Weapon = 1;
		public const int Armor = 2;
		public const int Ntele = 6;       // 傳送卷軸 (隨機/書籤)
		public const int Identify = 7;    // 鑑定卷軸
		public const int Res = 8;         // 復活卷軸
		public const int Choice = 14;     // 材料選擇
		public const int Sosc = 16;       // 變身卷軸
		public const int Blank = 28;      // 空白卷軸
		public const int Btele = 29;      // 祝福傳送卷軸
		public const int SpellBuff = 30;  // スペルスクロール(對自己時送 ObjectId)
		public const int Dai = 26;        // 武器強化卷軸
		public const int Zel = 27;        // 防具強化卷軸
		public const int FishingRod = 42;
	}
}
