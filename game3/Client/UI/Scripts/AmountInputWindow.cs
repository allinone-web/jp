using Godot;
using System;

namespace Client.UI
{
    /// <summary>
    /// 通用数量输入窗口
    /// 用于丢弃、交易、存取等需要指定数量的场景
    /// </summary>
    public partial class AmountInputWindow : GameWindow
    {
        private LineEdit _inputField;
        private Button _confirmBtn;
        private Label _promptLabel;

        // 回调动作：当用户点击确认时，将输入的数字返回
        public Action<int> OnAmountConfirmed;

        public override void _Ready()
        {
            base._Ready();

            _inputField = FindChild("InputField", true, false) as LineEdit;
            _confirmBtn = FindChild("ConfirmBtn", true, false) as Button;
            _promptLabel = FindChild("PromptLabel", true, false) as Label;

            if (_confirmBtn != null)
            {
                _confirmBtn.Pressed += OnConfirmPressed;
            }
        }

        public override void OnOpen(WindowContext context = null)
        {
            base.OnOpen(context);
            
            if (_inputField != null)
            {
                _inputField.Text = "1";
                _inputField.GrabFocus();
                _inputField.SelectAll();
            }

            if (context?.ExtraData is string prompt)
            {
                if (_promptLabel != null) _promptLabel.Text = prompt;
            }
            else if (context?.ExtraData is System.Collections.Generic.Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("prompt", out var p) && p != null && _promptLabel != null)
                    _promptLabel.Text = p.ToString();
                if (dict.TryGetValue("onConfirm", out var cb) && cb is Action<int> act)
                    OnAmountConfirmed = act;
            }
        }

        private void OnConfirmPressed()
        {
            if (_inputField == null) return;

            if (int.TryParse(_inputField.Text, out int amount) && amount > 0)
            {
                OnAmountConfirmed?.Invoke(amount);
                Close();
            }
        }

        // 辅助：快速清空回调，防止内存泄漏
        public override void Close()
        {
            base.Close();
            OnAmountConfirmed = null; 
        }
    }
}