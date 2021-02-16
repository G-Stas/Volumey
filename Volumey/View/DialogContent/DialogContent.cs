using System;
using System.Windows.Controls;
using MahApps.Metro.Controls.Dialogs;

namespace Volumey.View.DialogContent
{
	public abstract class DialogContent : UserControl
	{
		public abstract event Action<DialogContent> DialogResult;
		public CustomDialog ParentDialog => this.Parent as CustomDialog;
	}
}