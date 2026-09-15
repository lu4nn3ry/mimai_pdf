# PdfPig e dependências incorporadas

PdfPig 0.1.9 é distribuído sob Apache-2.0. Usamos os assemblies `net471`
originais do pacote NuGet, sem modificações. A abertura lê a estrutura do PDF;
`GetPage(n)` e `ContentOrderTextExtractor.GetText` são chamados apenas para a
página solicitada no modo de extração de texto.

Origem: https://www.nuget.org/packages/PdfPig/0.1.9

Revisão: `eb9a191e0d6beb27d67b89912473d9733cbb2f27` de https://github.com/UglyToad/PdfPig

Dependências obtidas dos respectivos pacotes em `https://api.nuget.org/v3-flatcontainer/`:

| Pacote | Versão | Assembly selecionado |
|---|---|---|
| Microsoft.Bcl.HashCode | 1.1.1 | net461 |
| System.Memory | 4.5.5 | net461 |
| System.Buffers | 4.5.1 | net461 |
| System.Runtime.CompilerServices.Unsafe | 4.5.3 | net461 |
| System.Numerics.Vectors | 4.5.0 | net46 |

Licenças e avisos originais de cada pacote acompanham os arquivos. Os hashes
dos assemblies constam em `SHA256SUMS.txt`. DLLs, licenças e avisos são incorporados
ao executável; nenhuma restauração NuGet ou conexão de rede é necessária no uso.
O aplicativo requer .NET Framework 4.7.1 ou superior.
