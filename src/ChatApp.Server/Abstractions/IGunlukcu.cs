namespace ChatApp.Server.Abstractions;

// Log yazma sozlesmesi. Sunucu kutuphanesi Serilog'u bilmez; uygulamasi ChatServer.exe'de.
// KURAL: mesaj icerikleri ve sifreler ASLA loga yazilmaz.
public interface IGunlukcu
{
    void Bilgi(string mesaj);
    void Uyari(string mesaj);
    void Hata(Exception hata, string mesaj);
}
