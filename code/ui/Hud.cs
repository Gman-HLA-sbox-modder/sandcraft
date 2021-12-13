using Sandbox.UI;

namespace Sandblox
{
	public partial class HudEntity : Sandbox.HudEntity<RootPanel>
	{
		public HudEntity()
		{
			if ( !IsClient )
				return;

			RootPanel.StyleSheet.Load( "/ui/Hud.scss" );

			RootPanel.AddChild<ChatBoxPlus>();
			RootPanel.AddChild<KillFeedCustom>();
		}
	}
}
