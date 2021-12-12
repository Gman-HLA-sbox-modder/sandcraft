using Sandbox.UI;

namespace Sandblox
{
	public partial class HudEntity : Sandbox.HudEntity<RootPanel>
	{
		public HudEntity()
		{
			if ( IsClient )
			{
				RootPanel.SetTemplate( "/hud.html" );
			}
		}
	}
}
