using System.Text.Json.Serialization;

namespace ChatApp.Protocol.Paketler;

// Istemci ile sunucu arasindaki her mesaj bir "paket"tir.
// Her paket tek satir JSON olarak gider; "tur" alani paketin tipini soyler:
//   {"tur":"GirisIstegi","KullaniciAdi":"ali","Sifre":"..."}
// Yeni bir paket tipi eklenince buraya da bir satir eklenir.
[JsonPolymorphic(TypeDiscriminatorPropertyName = "tur",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(KayitIstegi), "KayitIstegi")]
[JsonDerivedType(typeof(GirisIstegi), "GirisIstegi")]
[JsonDerivedType(typeof(MesajGonder), "MesajGonder")]
[JsonDerivedType(typeof(OkunduBildir), "OkunduBildir")]
[JsonDerivedType(typeof(GecmisIstegi), "GecmisIstegi")]
[JsonDerivedType(typeof(KullaniciSorgula), "KullaniciSorgula")]
[JsonDerivedType(typeof(EngelleIstegi), "EngelleIstegi")]
[JsonDerivedType(typeof(SifreDegistirIstegi), "SifreDegistirIstegi")]
[JsonDerivedType(typeof(KayitSonucu), "KayitSonucu")]
[JsonDerivedType(typeof(GirisSonucu), "GirisSonucu")]
[JsonDerivedType(typeof(SohbetListesi), "SohbetListesi")]
[JsonDerivedType(typeof(MesajKabulEdildi), "MesajKabulEdildi")]
[JsonDerivedType(typeof(MesajGeldi), "MesajGeldi")]
[JsonDerivedType(typeof(MesajReddedildi), "MesajReddedildi")]
[JsonDerivedType(typeof(MesajDurumuDegisti), "MesajDurumuDegisti")]
[JsonDerivedType(typeof(MesajlarOkundu), "MesajlarOkundu")]
[JsonDerivedType(typeof(GecmisCevabi), "GecmisCevabi")]
[JsonDerivedType(typeof(KullaniciBilgisi), "KullaniciBilgisi")]
[JsonDerivedType(typeof(CevrimiciDurumu), "CevrimiciDurumu")]
[JsonDerivedType(typeof(IslemSonucu), "IslemSonucu")]
[JsonDerivedType(typeof(HataBildirimi), "HataBildirimi")]
[JsonDerivedType(typeof(OturumSonlandi), "OturumSonlandi")]
public abstract record Paket;
