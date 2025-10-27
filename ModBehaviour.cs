// ------------------------------------------------------
// LuGuanModv1 - LuGuanBehaviour.cs (长按 L + 扣除能量/水分 版本)
// ------------------------------------------------------
using Duckov.Modding;             // ModBehaviour 基类
using UnityEngine;                // MonoBehaviour, Debug, Input, KeyCode, Transform, Time
using ItemStatsSystem;            // Item, Inventory
using Duckov.UI.DialogueBubbles;  // 对话气泡 API
using Cysharp.Threading.Tasks;    // UniTask (来自 UniTask.dll)
using System.Linq;                // 用于 Linq 查询背包
// using Duckov.Core;             // 可能需要引入 CharacterMainControl 的命名空间

namespace LuGuanModv1
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        // 卷纸的物品 ID
        private const int TOILET_PAPER_ID = 60;
        // 长按触发所需时间 (秒)
        private const float HOLD_DURATION_REQUIRED = 2.0f;
        // 扣除的能量值
        private const float ENERGY_COST = 20f;
        // 扣除的水分值
        private const float WATER_COST = 20f;

        // 长按状态变量
        private bool _isLKeyDown = false;
        private float _lKeyDownStartTime = 0f;
        private bool _actionTriggeredThisPress = false;

        // Mod 初始化
        protected override void OnAfterSetup() //
        {
            base.OnAfterSetup();
            Debug.Log("###################[LuGuanModv1] 欢迎使用 LuGuanModv1！####################");
            Debug.Log("[LuGuanModv1] Mod 初始化完成。");
            Debug.Log($"[LuGuanModv1] Mod 已加载！长按 L 键 {HOLD_DURATION_REQUIRED} 秒来获得快乐 (需要消耗卷纸 ID:{TOILET_PAPER_ID} 并扣除 {ENERGY_COST} 能量和 {WATER_COST} 水分)。");
        }

        // 每帧调用
        void Update()
        {
            // --- 检测 L 键长按 ---
            if (Input.GetKeyDown(KeyCode.L))
            {
                _isLKeyDown = true;
                _lKeyDownStartTime = Time.time;
                _actionTriggeredThisPress = false;
            }

            if (_isLKeyDown && Input.GetKey(KeyCode.L))
            {
                float holdTime = Time.time - _lKeyDownStartTime;
                if (holdTime >= HOLD_DURATION_REQUIRED && !_actionTriggeredThisPress)
                {
                    Debug.Log($"[LuGuanModv1] L 键已长按 {HOLD_DURATION_REQUIRED} 秒，触发动作！");
                    _actionTriggeredThisPress = true;
                    CheckInventoryAndShowBubble();
                }
            }

            if (Input.GetKeyUp(KeyCode.L))
            {
                _isLKeyDown = false;
            }
        }

        // 检查背包并显示气泡的方法
        private void CheckInventoryAndShowBubble()
        {
            // 获取主角色
            CharacterMainControl mainCharacter = CharacterMainControl.Main; //
            if (mainCharacter == null)
            {
                Debug.LogError("[LuGuanModv1] 无法获取主角色！");
                return;
            }

            // --- 访问玩家背包 (假设方式，可能需要修改) ---
            Inventory playerInventory = mainCharacter.GetComponentInChildren<Inventory>();
            if (playerInventory == null)
            {
                Debug.LogError("[LuGuanModv1] 无法获取玩家背包！请检查获取背包的代码。");
                ShowBubbleAsync(mainCharacter.transform, "错误：找不到背包！");
                return;
            }

            // --- 查找卷纸 ---
            Item toiletPaper = playerInventory.Content.FirstOrDefault(item => item != null && item.TypeID == TOILET_PAPER_ID); //

            // --- 根据查找结果执行操作 ---
            if (toiletPaper != null)
            {
                Debug.Log($"[LuGuanModv1] 在背包中找到卷纸 (ID: {TOILET_PAPER_ID})。");
                // 异步执行消耗物品、扣除状态和显示快乐气泡
                ConsumeItemDeductStatsAndShowHappyBubble(toiletPaper, playerInventory, mainCharacter); // 传递 mainCharacter
            }
            else
            {
                Debug.LogWarning($"[LuGuanModv1] 背包中没有找到卷纸 (ID: {TOILET_PAPER_ID})！");
                // 异步显示提示气泡
                ShowBubbleAsync(mainCharacter.transform, "没有找到卷纸！打断施法！");
            }
        }

        // 异步消耗物品、扣除状态并显示快乐气泡
        private void ConsumeItemDeductStatsAndShowHappyBubble(Item itemToConsume, Inventory inventory, CharacterMainControl player)
        {
            UniTask.Void(async () =>
            {
                Transform playerTransform = player.transform; // 获取 Transform
                try
                {
                    // 1. 先显示气泡
                    await DialogueBubblesManager.Show("撸管好爽，撸管使我快乐", playerTransform, duration: 2f); //
                    Debug.Log("[LuGuanModv1] 快乐气泡已显示。");

                    // 2. 再消耗物品 (确保物品仍然有效)
                    if (itemToConsume == null || itemToConsume.gameObject == null)
                    {
                        Debug.LogWarning("[LuGuanModv1] 尝试消耗卷纸时，物品已失效。");
                        return;
                    }

                    bool consumed = false; // 标记是否成功消耗
                    if (itemToConsume.Stackable && itemToConsume.StackCount > 1) //
                    {
                        itemToConsume.StackCount = itemToConsume.StackCount - 1; // - 使用属性赋值
                        Debug.Log($"[LuGuanModv1] 卷纸数量减少，剩余: {itemToConsume.StackCount}");
                        consumed = true;
                    }
                    else
                    {
                        bool removed = inventory.RemoveItem(itemToConsume); //
                        if (removed)
                        {
                            Destroy(itemToConsume.gameObject); //
                            Debug.Log("[LuGuanModv1] 卷纸已消耗。");
                            consumed = true;
                        }
                        else
                        {
                            Debug.LogError("[LuGuanModv1] 尝试从背包移除卷纸失败！");
                            Destroy(itemToConsume.gameObject);
                            Debug.LogWarning("[LuGuanModv1] 尝试直接销毁卷纸 GameObject。");
                            // 即使移除失败，如果 Destroy 成功也算消耗
                            consumed = true;
                        }
                    }

                    // 3. 如果物品成功消耗，则扣除能量和水分
                    if (consumed)
                    {
                        // 使用 AddEnergy 和 AddWater 方法，传入负值进行扣除
                        player.AddEnergy(-ENERGY_COST);
                        player.AddWater(-WATER_COST);
                        Debug.Log($"[LuGuanModv1] 已扣除 {ENERGY_COST} 能量和 {WATER_COST} 水分。" +
                                  $" 当前能量: {player.CurrentEnergy}, 当前水分: {player.CurrentWater}"); // - 读取当前值
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LuGuanModv1] 消耗物品、扣除状态或显示气泡时出错: {e.Message}\n{e.StackTrace}");
                }
            });
        }

        // 通用的异步显示气泡方法
        private void ShowBubbleAsync(Transform targetTransform, string message)
        {
            UniTask.Void(async () =>
            {
                try
                {
                    await DialogueBubblesManager.Show(message, targetTransform, duration: 2f); //
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[LuGuanModv1] 显示提示气泡时出错: {e.Message}\n{e.StackTrace}");
                }
            });
        }

        // Mod 卸载/停用时调用
        protected override void OnBeforeDeactivate() //
        {
            Debug.Log("[LuGuanModv1] Mod 即将卸载。");
            base.OnBeforeDeactivate();
        }
    }
}