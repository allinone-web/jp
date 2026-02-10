using Godot;
using System;

namespace Client.Data
{
    // 【核心保留】继承 GodotObject
    public partial class InventoryItem : GodotObject
    {
        public int ObjectId { get; set; }      // 唯一ID
        public int ItemId { get; set; }        // 种类ID
        public string Name { get; set; }       // 名字
        public int Count { get; set; }         // 数量
        
        // --- 图像与类型 ---
        public int GfxId { get; set; }         // 原始图标ID
        
        // 【新增】UI 兼容属性：Lineage 中 GfxId 即图标ID
        public int IconId 
        { 
            get => GfxId; 
            set => GfxId = value; 
        }

        // 【新增】物品部位类型 (0:道具, 1:头盔... 对应 CharacterWindow 的映射)
        public int Type { get; set; }          

        // --- 状态 ---
        public bool IsEquipped { get; set; }   // 穿戴状态
        public int Bless { get; set; }         // 祝福
        public int Ident { get; set; }         // 鉴定

        /// <summary>鑑定後由 Opcode 111 擴充資料解析出的介紹（攻擊/防禦/職業/重量等），未鑑定或無資料為 null/空。</summary>
        public string DetailInfo { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Count})";
        }
    }
}