    public static class Player_Rizolver
    {
		static DependencyA depA = null;
		static Pepe.DependencyC depC = null;

        public static void Injek(this Player instance)
        {
				Context container = instance.GetComponent<Context>();
				if(container == null)
					container = Project.CurrentScene.FindObjectOfType<SceneContext>();
				if(container == null)
					throw new System.Exception("No SceneContext found in scene, please add one");
				depA = container.Resolve<DependencyA>();
				depC = container.Resolve<Pepe.DependencyC>();
				Pepe.DependencyC_Rizolver.Injek(depC);

			instance.Construc(depA, depC);
        }
    }
namespace Pepe
{
    public static class DependencyC_Rizolver
    {
		static DependencyB depB = null;

        public static void Injek(this DependencyC instance)
        {
					Context container = Project.CurrentScene.FindObjectOfType<SceneContext>();
				if(container == null)
					throw new System.Exception("No SceneContext found in scene, please add one");
				depB = container.Resolve<DependencyB>();

			instance.Inject(depB);
        }
    }
}
