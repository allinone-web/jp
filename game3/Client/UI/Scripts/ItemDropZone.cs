using Godot;

namespace Client.UI
{
	/// <summary>
	/// 覆蓋遊戲畫面的透明區域，用於接收「從背包拖動物品到地面」的放下操作；放下時發送 item_dropped(objectId)。
	/// </summary>
	public partial class ItemDropZone : Control
	{
		[Signal] public delegate void ItemDroppedEventHandler(int itemObjectId);

		public override void _Ready()
		{
			SetAnchorsPreset(Control.LayoutPreset.FullRect);
			MouseFilter = MouseFilterEnum.Stop;
			// 透明、不阻擋點擊時設為 Ignore，但拖放時 Godot 仍會對有 _CanDropData 的節點做檢測
		}

		public override bool _CanDropData(Vector2 atPosition, Variant data)
		{
			if (data.VariantType != Variant.Type.Dictionary) return false;
			var dict = data.AsGodotDictionary();
			if (!dict.ContainsKey("type") || dict["type"].AsString() != "item") return false;
			if (!dict.ContainsKey("id")) return false;
			return true;
		}

		public override void _DropData(Vector2 atPosition, Variant data)
		{
			if (data.VariantType != Variant.Type.Dictionary) return;
			var dict = data.AsGodotDictionary();
			if (!dict.ContainsKey("id")) return;
			int id = dict["id"].AsInt32();
			EmitSignal(SignalName.ItemDropped, id);
		}
	}
}
