using GameProject.ItemManagement;
using Unity.Entities;

public static class BakingDataExtensions
{
    public static ItemAssetDataReference GetOrCreateItemAssetDataRef(this IBaker baker, ItemAssetAuthoring itemAssetAuthoring)
    {
        var assetRef = BlobAssetReference<ItemAssetData>.Create(new ItemAssetData {
            PurchaseCost = itemAssetAuthoring.PurchaseCost
        });
        baker.AddBlobAssetWithCustomHash(ref assetRef, itemAssetAuthoring.Guid.Hash128Value);
        return new ItemAssetDataReference {
            Value = assetRef
        };
    }
}