# Assistente de parâmetros — Revit (AutoDocumentation)

Esta pasta destina-se a ser enviada a colegas ou a outro computador **junto com o ficheiro de instalação** (`.exe`) gerado pelo build.

## O que deve conter a pasta

- **Instalador:** `AutoDocumentation-Revit{ANO}-{VERSÃO}-Setup.exe` (o nome inclui o ano do Revit e a versão do pacote).
- **Este ficheiro** (`README.md` ou cópia deste documento), para quem recebe saber o que instalar e como.

## Requisitos

- **Windows** 64 bits.
- **Autodesk Revit** instalado na **mesma versão** indicada no nome do instalador (ex.: `Revit2025` → Revit 2025).
- Permissões para escrever na pasta de add-ins do **utilizador** (`%AppData%\Autodesk\Revit\Addins\`).

O add-in é compilado para **.NET 8**; o Revit 2025 (e versões com suporte oficial) carrega este tipo de add-ins — **não é necessário** instalar o .NET SDK à parte para **usar** o instalador.

## Instalação

1. **Feche o Revit** (e qualquer sessão que esteja a usar o add-in antigo).
2. Faça duplo clique no ficheiro **`…-Setup.exe`**.
3. Siga o assistente até ao fim (a instalação é por utilizador, sem pedir administrador de sistema).
4. Abra o **Revit** na versão correspondente e confirme que o comando **«Assistente de parâmetros»** aparece no sítio habitual (ribbon / add-ins, conforme a configuração do Revit).

### Onde ficam os ficheiros

Por defeito, o instalador coloca:

- A DLL e ficheiros do add-in em:  
  `%AppData%\Autodesk\Revit\Addins\{ANO}\AutoDocumentation\`
- O manifesto `.addin` em:  
  `%AppData%\Autodesk\Revit\Addins\{ANO}\`

(`%AppData%` corresponde normalmente a `C:\Users\<utilizador>\AppData\Roaming\`.)

## Atualização

Para instalar uma **versão mais recente**, volte a executar o novo `.exe` com o Revit fechado. O instalador substitui os ficheiros da mesma aplicação (mesmo identificador de produto).

## Desinstalação

- **Definições** → **Aplicações** → **Aplicações e funcionalidades** (ou «Programas e funcionalidades» no Painel de controlo), procure a entrada do assistente / AutoDocumentation e desinstale.

Ou elimine manualmente a pasta `AutoDocumentation` e o ficheiro `AutoDocumentation.addin` na pasta `Addins\{ANO}\` referida acima (com o Revit fechado).

## Suporte técnico (quem desenvolve)

- Repositório / equipa interna: use a documentação do projecto e o código-fonte.
- Em caso de falha ao arrancar o Revit após instalar, confirme a **versão do Revit**, que não há outro `.addin` em conflito e que o antivírus não bloqueou a DLL.

---

*Documento para distribuição com o pacote de instalação. Versão do pacote alinhada com o nome do ficheiro `Setup.exe`.*
