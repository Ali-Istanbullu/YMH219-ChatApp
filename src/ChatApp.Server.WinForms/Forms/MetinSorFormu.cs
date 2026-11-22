namespace ChatApp.Server.WinForms.Forms;

// Tek satir metin soran kucuk pencere (ornek: yasaklama sebebi). WinForms'ta hazir InputBox yok.
public sealed class MetinSorFormu : Form
{
    private readonly TextBox _kutu;

    private MetinSorFormu(string baslik, string soru, int enFazlaUzunluk)
    {
        Text = baslik;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.75f);

        var etiket = new Label { Text = soru, AutoSize = true, Margin = new Padding(3, 3, 3, 6) };
        _kutu = new TextBox { Width = 360, MaxLength = enFazlaUzunluk };
        var tamam = new Button { Text = "Tamam", DialogResult = DialogResult.OK, AutoSize = true };
        var iptal = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, AutoSize = true };
        var butonlar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Width = 366 };
        butonlar.Controls.AddRange([iptal, tamam]);

        var dikey = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(12) };
        dikey.Controls.AddRange([etiket, _kutu, butonlar]);
        Controls.Add(dikey);
        AcceptButton = tamam;
        CancelButton = iptal;
    }

    // Iptal edilirse null.
    public static string? Sor(IWin32Window sahip, string baslik, string soru, int enFazlaUzunluk)
    {
        using var form = new MetinSorFormu(baslik, soru, enFazlaUzunluk);
        return form.ShowDialog(sahip) == DialogResult.OK ? form._kutu.Text : null;
    }
}
