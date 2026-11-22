namespace ChatApp.Client.Abstractions;

// Log yazma sozlesmesi. Istemci kutuphanesi Serilog'u bilmez; uygulamasi ChatApp.exe'de.
// KURAL: mesaj icerikleri ve sifreler ASLA loga yazilmaz.
public interface IGunlukcu
{
    void Bilgi(string mesaj);
    void Uyari(string mesaj);
    void Hata(Exception hata, string mesaj);
}
