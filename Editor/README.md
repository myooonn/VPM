# Ezpzoptimizer

VRCアバターのテクスチャを一括で軽量化するUnity Editorツールです。  
A Unity Editor tool to batch-optimize textures on VRChat avatars.

## 対応環境 / Requirements

- Unity 2022.3.22f1+
- VRChat SDK (optional - SDK無しでも動作します)

## インストール / Install

1. `EzPzOptimizer.unitypackage` をUnityプロジェクトにインポート
2. メニューから **Tools > Ezpzoptimizer** を開く

## 使い方 / Usage

1. **アバターを選択**: Hierarchyからアバターの最上位GameObjectをドラッグ
2. **テクスチャ検出**: 「テクスチャ検出」ボタンをクリック
3. **テクスチャを選択**: 最適化したいテクスチャにチェック
4. **設定を選ぶ**:
   - **ターゲットサイズ**: 128 / 256 / 512 / 1024
   - **圧縮形式**: 自動 / 標準 / 高品質 / 軽量
   - **Quest最適化**: Android向け圧縮を同時設定
5. **最適化実行**: ボタンを押して一括最適化

## 機能 / Features

| 機能 | Feature |
|------|---------|
| テクスチャ自動検出 | Auto-detect textures from avatar |
| サムネイル付き一覧 | Thumbnail texture list |
| VRAM使用量表示 | VRAM usage estimation |
| Quest対応 | Quest (Android) support |
| **マテリアル最適化** | **Material Optimization** |
| 不要プロパティ削除 | Clean unused shader properties |
| 重複マテリアル統合 | **Merge Duplicate Materials** (DrawCall Reduction) |
| **パフォーマンス診断** | **Performance Diagnostics** |
| Missing Script削除 | Clean broken Missing Scripts |
| 不要アセット修正 | Disable empty Audio/Particles |
| PhysBone制限確認 | Review excessive PhysBones |
| バックアップ/復元 | Backup & Restore |
| 日/英 切り替え | JP/EN language toggle |  

