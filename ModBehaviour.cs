// ------------------------------------------------------
// LuGuanModv1 - LuGuanBehaviour.cs (模拟物品使用 - 等待时间版)
// ------------------------------------------------------
using Duckov.Modding;             // ModBehaviour 基类
using UnityEngine;                // MonoBehaviour, Debug, Input, KeyCode, Transform, Time
using ItemStatsSystem;            // Item, Inventory, ItemAssetsCollection
using Duckov.UI.DialogueBubbles;  // 对话气泡 API
using Cysharp.Threading.Tasks;    // UniTask (来自 UniTask.dll)
using System.Linq;                // 用于 Linq 查询背包
using Duckov.Buffs;               // Buff (仅用于可能的检查)
using System;                     // TimeSpan

namespace LuGuanModv1
{
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        // 物品 ID
        private const int TOILET_PAPER_ID = 60;
        private const int LOLLIPOP_ID = 1181;       //
        private const int PAINKILLER_ID = 409;      //
        // 长按触发时间
        private const float HOLD_DURATION_REQUIRED = 2.0f;
        // 消耗
        private const float ENERGY_COST = 20f;
        private const float WATER_COST = 20f;
        // Buff 持续时间 (由物品使用决定)
        private const float HAPPY_BUFF_EXPECTED_DURATION = 90.0f; //
        // 气泡持续时间
        private const float BUBBLE_DURATION_1 = 5f;
        private const float BUBBLE_DURATION_2 = 3f;
        private const float BUBBLE_DURATION_3 = 5f;
        // **新增：估计的物品使用时间 (秒) - 你需要根据游戏实际情况调整这些值!**
        private const float LOLLIPOP_USE_TIME = 1.5f;     // 棒棒糖估计使用时间
        private const float PAINKILLER_USE_TIME = 3.0f;   // 镇痛剂估计使用时间


        // 长按状态
        private bool _isLKeyDown = false;
        private float _lKeyDownStartTime = 0f;
        private bool _actionTriggeredThisPress = false;
        private bool _isSequenceRunning = false;

        // Mod 初始化
        protected override void OnAfterSetup() //
        {
            base.OnAfterSetup();
            Debug.Log("###################[LuGuanModv1] 欢迎使用 LuGuanModv1！####################");
            Debug.Log($"[LuGuanModv1] Mod 已加载！长按 L 键 {HOLD_DURATION_REQUIRED} 秒触发效果。");
        }

        // 每帧调用，检测长按
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                _isLKeyDown = true;
                _lKeyDownStartTime = Time.time;
                _actionTriggeredThisPress = false;
            }

            if (_isLKeyDown && Input.GetKey(KeyCode.L))
            {
                float holdTime = Time.time - _lKeyDownStartTime;
                if (holdTime >= HOLD_DURATION_REQUIRED && !_actionTriggeredThisPress && !_isSequenceRunning)
                {
                    Debug.Log($"[LuGuanModv1] L 键长按达到 {HOLD_DURATION_REQUIRED} 秒，尝试触发序列...");
                    _actionTriggeredThisPress = true;
                    _isSequenceRunning = true;
                    StartLuGuanSequence();
                }
            }
            if (Input.GetKeyUp(KeyCode.L))
            {
                _isLKeyDown = false;
            }
        }

        // 启动效果序列 (检查背包)
        private void StartLuGuanSequence()
        {
            CharacterMainControl mainCharacter = CharacterMainControl.Main; //
            if (mainCharacter == null) { Debug.LogError("[LuGuanModv1] 无法获取主角色！"); _isSequenceRunning = false; return; }

            Inventory playerInventory = mainCharacter.GetComponentInChildren<Inventory>(); // 假设获取背包方式
            if (playerInventory == null) { Debug.LogError("[LuGuanModv1] 无法获取玩家背包！"); ShowBubbleAsync(mainCharacter.transform, "错误：找不到背包！").Forget(); _isSequenceRunning = false; return; }

            Item toiletPaper = playerInventory.Content.FirstOrDefault(item => item != null && item.TypeID == TOILET_PAPER_ID); //

            if (toiletPaper != null)
            {
                Debug.Log($"[LuGuanModv1] 找到卷纸，开始执行效果序列...");
                ExecuteLuGuanSequenceAsync(toiletPaper, playerInventory, mainCharacter).Forget(); // 使用 Forget() 启动异步序列
            }
            else
            {
                Debug.LogWarning($"[LuGuanModv1] 背包中没有找到卷纸！");
                ShowBubbleAsync(mainCharacter.transform, "没有找到卷纸！打断施法！").Forget(); // 使用 Forget()
                _isSequenceRunning = false;
            }
        }

        // 异步执行完整的效果序列
        private async UniTask ExecuteLuGuanSequenceAsync(Item itemToConsume, Inventory inventory, CharacterMainControl player)
        {
            Transform playerTransform = player.transform;
            bool consumed = false;
            try
            {
                // --- 1. 消耗卷纸 ---
                if (itemToConsume == null || itemToConsume.gameObject == null) { Debug.LogWarning("[LuGuanModv1] 物品在序列开始时已失效。"); return; }
                if (itemToConsume.Stackable && itemToConsume.StackCount > 1) { itemToConsume.StackCount--; consumed = true; } //
                else { bool removed = inventory.RemoveItem(itemToConsume); if (removed) { Destroy(itemToConsume.gameObject); consumed = true; } else { Debug.LogError("[LuGuanModv1] 移除卷纸失败！"); Destroy(itemToConsume.gameObject); consumed = true; } } //
                if (!consumed) { Debug.LogError("[LuGuanModv1] 卷纸消耗失败，中断序列！"); return; }
                Debug.Log("[LuGuanModv1] 卷纸已消耗。");

                // --- 2. 扣除状态 ---
                player.AddEnergy(-ENERGY_COST); //
                player.AddWater(-WATER_COST);   //
                Debug.Log($"[LuGuanModv1] 已扣除 {ENERGY_COST} 能量和 {WATER_COST} 水分。");

                // --- 3. 显示气泡1 并 模拟使用棒棒糖 (带等待时间) ---
                Debug.Log("[LuGuanModv1] 显示气泡1 并 模拟使用棒棒糖...");
                ShowBubbleAsync(playerTransform, "虽然很艰难，但还是出来了...", BUBBLE_DURATION_1).Forget(); // 不等待气泡结束
                await SimulateItemUse(player, LOLLIPOP_ID, LOLLIPOP_USE_TIME); // **等待模拟使用完成**

                // --- 4. 等待 快乐 Buff 预期持续时间 ---
                Debug.Log($"[LuGuanModv1] 等待快乐效果 ({HAPPY_BUFF_EXPECTED_DURATION} 秒)...");
                await UniTask.Delay(TimeSpan.FromSeconds(HAPPY_BUFF_EXPECTED_DURATION));

                // --- 5. 显示气泡 2 ---
                Debug.Log("[LuGuanModv1] 显示气泡 2...");
                await DialogueBubblesManager.Show("那么，人生的意义是什么呢......", playerTransform, duration: BUBBLE_DURATION_2); //

                // --- 6. 显示气泡 3 并 模拟使用镇痛剂 (带等待时间) ---
                Debug.Log("[LuGuanModv1] 显示气泡 3 并 模拟使用镇痛剂...");
                ShowBubbleAsync(playerTransform, "我已进入贤者时间", BUBBLE_DURATION_3).Forget(); // 不等待气泡结束
                await SimulateItemUse(player, PAINKILLER_ID, PAINKILLER_USE_TIME); // **等待模拟使用完成**

                Debug.Log("[LuGuanModv1] 效果序列执行完毕。");

            }
            catch (Exception e) { Debug.LogError($"[LuGuanModv1] 执行效果序列时出错: {e.Message}\n{e.StackTrace}"); }
            finally { _isSequenceRunning = false; } // 确保序列状态被重置
        }

        // **修改:** 模拟使用指定 ID 物品的方法 (加入背包 + 等待使用时间)
        private async UniTask SimulateItemUse(CharacterMainControl player, int itemIdToUse, float estimatedUseTime = 2.0f)
        {
            if (player == null) { Debug.LogError($"[LuGuanModv1] SimulateItemUse: Player is null!"); return; }

            Inventory playerInventory = player.GetComponentInChildren<Inventory>(); // 假设
            if (playerInventory == null) { Debug.LogError($"[LuGuanModv1] SimulateItemUse: 无法获取玩家背包！(Item ID: {itemIdToUse})"); return; }

            Item tempItem = null;
            bool addedToInventory = false;
            try
            {
                tempItem = await ItemAssetsCollection.InstantiateAsync(itemIdToUse); //
                if (tempItem != null)
                {
                    Debug.Log($"[LuGuanModv1] 临时创建物品 {tempItem.DisplayName} (ID: {itemIdToUse})。");
                    addedToInventory = playerInventory.AddItem(tempItem); //
                    if (addedToInventory)
                    {
                        Debug.Log($"[LuGuanModv1] 临时物品 {tempItem.DisplayName} 已添加到背包。");
                        player.UseItem(tempItem); //
                        Debug.Log($"[LuGuanModv1] 已调用 player.UseItem() 启动使用临时物品 {tempItem.DisplayName}。");

                        // **等待预估的使用时间**
                        Debug.Log($"[LuGuanModv1] 等待预估使用时间: {estimatedUseTime} 秒...");
                        await UniTask.Delay(TimeSpan.FromSeconds(estimatedUseTime));
                        Debug.Log($"[LuGuanModv1] 等待结束，继续清理。");
                    }
                    else { Debug.LogError($"[LuGuanModv1] 无法将临时物品 {tempItem.DisplayName} 添加到背包！"); if (tempItem?.gameObject != null) Destroy(tempItem.gameObject); tempItem = null; }
                }
                else { Debug.LogError($"[LuGuanModv1] 无法实例化用于模拟使用的物品 (ID: {itemIdToUse})！"); }
            }
            catch (Exception e) { Debug.LogError($"[LuGuanModv1] 模拟使用物品 (ID: {itemIdToUse}) 时出错: {e.Message}\n{e.StackTrace}"); }
            finally // 确保清理
            {
                // 不需要之前的短延迟了，因为我们已经在 try 块中等待了 estimatedUseTime
                if (tempItem != null && tempItem.gameObject != null) // 再次检查，UseItem 可能已销毁
                {
                    if (addedToInventory)
                    {
                        // 检查物品是否还在背包中
                        Item itemInInventory = playerInventory.Content.FirstOrDefault(i => i != null && i.GetInstanceID() == tempItem.GetInstanceID());
                        if (itemInInventory != null)
                        {
                            bool removed = playerInventory.RemoveItem(itemInInventory); //
                            Debug.Log($"[LuGuanModv1] 清理：尝试从背包移除临时物品 {itemInInventory.DisplayName}，结果: {removed}");
                            if (removed && itemInInventory.gameObject != null) Destroy(itemInInventory.gameObject); //
                        }
                    }
                    // 最终保险销毁
                    if (tempItem != null && tempItem.gameObject != null)
                    {
                        Debug.Log($"[LuGuanModv1] 清理：销毁临时物品 {tempItem.DisplayName} 的 GameObject。");
                        Destroy(tempItem.gameObject); //
                    }
                }
            }
        }

        // 通用的异步显示气泡方法 (返回 UniTask)
        private async UniTask ShowBubbleAsync(Transform targetTransform, string message, float duration = 2f)
        {
            try { await DialogueBubblesManager.Show(message, targetTransform, duration: duration); } //
            catch (System.Exception e) { Debug.LogError($"[LuGuanModv1] 显示提示气泡时出错: {e.Message}\n{e.StackTrace}"); }
        }

        // Mod 卸载/停用时调用
        protected override void OnBeforeDeactivate() //
        {
            Debug.Log("[LuGuanModv1] Mod 即将卸载。");
            base.OnBeforeDeactivate();
        }
    }
}