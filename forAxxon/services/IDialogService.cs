using System.Threading.Tasks;

namespace forAxxon.Services;

public enum DialogButtons { OK, YesNo, YesNoCancel }
public enum DialogResult { None, OK, Yes, No, Cancel }

public interface IDialogService
{
    Task<DialogResult> ShowConfirmationAsync(string title, string message, DialogButtons buttons);
}