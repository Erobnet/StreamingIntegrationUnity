using Unity.Entities;

namespace GameProject.Common.Baking
{
    public interface IDeclareBakeDependencies
    {
        public void DeclareDependencies(IBaker baker);
    }
}