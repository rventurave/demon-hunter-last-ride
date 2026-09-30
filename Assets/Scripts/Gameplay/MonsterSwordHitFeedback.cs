using UnityEngine;

namespace JapaneseDemonHunter.Gameplay
{
    /// <summary>Brief tint on existing meshes; restores each renderer's original property block.</summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSwordHitFeedback : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float flashDuration = 0.12f;
        [SerializeField] private Color hitColor = new Color(1f, 0.4f, 0.3f);
        [SerializeField, Range(0f, 1f)] private float tintAmount = 0.45f;
        private Renderer[] meshes;
        private MaterialPropertyBlock[] originalBlocks;
        private bool flashing;
        private float restoreAt;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        public void ShowHit()
        {
            if (meshes == null)
            {
                meshes = GetComponentsInChildren<Renderer>();
                originalBlocks = new MaterialPropertyBlock[meshes.Length];
                for (int i = 0; i < meshes.Length; i++) originalBlocks[i] = new MaterialPropertyBlock();
            }
            if (!flashing)
            {
                for (int i = 0; i < meshes.Length; i++)
                {
                    Renderer mesh = meshes[i];
                    if (mesh == null || mesh.sharedMaterial == null) continue;
                    mesh.GetPropertyBlock(originalBlocks[i]);
                    int property = mesh.sharedMaterial.HasProperty(BaseColor) ? BaseColor : ColorProperty;
                    if (!mesh.sharedMaterial.HasProperty(property)) continue;
                    var block = new MaterialPropertyBlock();
                    mesh.GetPropertyBlock(block);
                    Color original = block.HasColor(property) ? block.GetColor(property) : mesh.sharedMaterial.GetColor(property);
                    block.SetColor(property, Color.Lerp(original, hitColor, tintAmount));
                    mesh.SetPropertyBlock(block);
                }
            }
            flashing = true;
            restoreAt = Time.time + flashDuration;
        }

        private void Update()
        {
            if (flashing && Time.time >= restoreAt) Restore();
        }

        private void OnDisable() => Restore();

        private void Restore()
        {
            if (!flashing || meshes == null) return;
            for (int i = 0; i < meshes.Length; i++)
                if (meshes[i] != null) meshes[i].SetPropertyBlock(originalBlocks[i]);
            flashing = false;
        }
    }
}
