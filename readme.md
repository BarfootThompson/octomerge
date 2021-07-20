# octomerge

Simple command line templating with [Hashicorp Vault](https://www.vaultproject.io/) integration powered by [Octostache](https://github.com/OctopusDeploy/Octostache). You do not need to use Octopus Deploy to benefit from it.

## Why?

Initially I was working on some Octopus Deploy based CI/CD pipelines, and I realised that the full release cycle is just too slow for the development. I wanted to build a container from source code, push it to the registry, deploy to the kubernetes cluster and run tests on it all from my development box, forgoing the CI server and Octopus Deploy. However, we have already been writing templates for Octopus Deploy, so in order to leverage those, I needed a way to merge them locally. This is how `octomerge` was born. I ended up using it (along with other tooling) for generic docker and kubernetes deployment. A kubernetes manifest, a `docker-compose.yaml` or any other configuration file that the deployment requires, but the content of which could vary can benefit from templating. I addition, if those have to have any sensitive information embedded `octomerge` can fetch it from Vault. This trivially allows putting all the configuration files mentioned above into source control, while keeping the secrets out of it.

## How?

You need to specify the template to parse and the variable values to substitute in the form of a [toml](https://github.com/toml-lang/toml) file. You can parse all templates in a specified subfolder recursively. Your toml file can contain references to Vault secrets which will be fetched for you during merging. `octomerge` will use your Vault token from one of the two standard locations: either from `VAULT_TOKEN` environment variable, or from `~/.vault-token` file, which is created by `vault login` command. You also need Vault address in `VAULT_ADDR` environment variable.

## Templating Language

Please refer to [Octopus Deploy templates](https://octopus.com/docs/projects/variables/variable-substitutions) documentation for the details.

## Installation

Download from <https://github.com/AndrewSav/octomerge/releases>, and unarchive. Put on `PATH` if desirable.

## Usage

There are two modes in which this utility can run, a single-file mode and a multi-file mode. 

### Single file mode

In single-file mode you should provide a flat toml file like this:

```toml
"Seq:ServerUrl" = "http://localhost:5341"
"Seq:ApiKey" = "abcdefghijklmnopqrstuvwxyz"
"Seq:MinimumLevel" = "Information"
"OriginUrl:Url" = "https://myapp.mydomain.tld"
"OAuth:OAuthBaseUrl" = "https://oauth.mydomain.tld"
"OAuth:OAuthTokenEndpoint" = "oauth/token"
"OAuth:OAuthConsumerKey" = "F63BBF789C6111666A800F0E9B5D78BC"
"OAuth:OAuthConsumerSecret" = "B28D700BBDD1629FD88ED46AE36F2EEA"
"OAuth:OAuthTokenInterval" = "60"
"OAuth:OAuthIsRequired" = "true"
```

Here we can see variable names to substitute on the left of the equal sign, and their respective values on the right. 

The single-file form is invoked like this:

```powershell
octomerge variables.toml file.txt.template result.txt [-q] [-p] [-s] [-v] [-d]
```

Here the program will read `variables.toml` for single-file mode, will read `file.txt.template` template and will write the results into `result.txt`. See explanation of other flags below.

### Multifile mode

In the multi-file mode, the toml file is broken down on individual toml tables, each of which corresponds to a template file to merge. For example:

```toml
["deployment.yaml"]
"Seq:ServerUrl" = "http://localhost:5341"
"Seq:ApiKey" = "abcdefghijklmnopqrstuvwxyz"
"Seq:MinimumLevel" = "Information"
"OriginUrl:Url" = "https://myapp.mydomain.tld"
"OAuth:OAuthBaseUrl" = "https://oauth.mydomain.tld"
"OAuth:OAuthTokenEndpoint" = "oauth/token"
"OAuth:OAuthConsumerKey" = "F63BBF789C6111666A800F0E9B5D78BC"
"OAuth:OAuthConsumerSecret" = "B28D700BBDD1629FD88ED46AE36F2EEA"
"OAuth:OAuthTokenInterval" = "60"
"OAuth:OAuthIsRequired" = "true"
["infrastructure.yaml"]
"host" = "myhost.local"
["conf-k8s\secrets\search-sphinx\sphinx.conf"]
"ConnectionString" = "bla"
```

In this example the program will read `deployment.yaml.template` file and produce `deployment.yaml` file, it will read `infrastructure.yaml.template` file and produce `infrastructure.yaml` file, it will read `conf-k8s\secrets\search-sphinx\sphinx.conf.template` file and produce `conf-k8s\secrets\listing-search-sphinx\sphinx.conf` file.

 To run the program in the multi-file mode, use the `-x` switch:

```powershell
octomerge variables.toml -x [-q] [-g] [-p] [-s] [-v] [-d]
```

In this case the result files to write to are given by the table names inside the specified on the command line `variables.toml` file, and the templates are assumed to have the same names as each result files with `.template` extension as was demonstrated above.

### Unused variables detection

In order to help author correct templates and toml files `octomerge` detects situations when there is a mismatch between variables specified in the toml file, and variables specified in a template. There are two situations possible:

- A variable is in the toml file but not in the template. Since in this situation it is still possible to substitute all variables used in a template, this will result in a warning, not a error. `-s` switch makes these warnings into errors and terminates. `-q` suppresses those warnings, so that they are not displayed
- A variable is in the template but not in the toml file. In this case the result of transformation will still have some not substituted variables in it, which in most case is not what is desired. This situation produce a error and terminate. Use `-p` switch to make these errors warnings and do not terminate. This is useful, if you want to do some post-processing on partial templates

### Global variables

Sometimes you can find yourself in a situation, when in multi-file mode same variables are used over and over again in multiple template, for example:

```toml
["kubernetes\05-dashboard\ingress.yaml"]
"host" = "k8s-dashboard2.domain.tld"
["powershell\provision.ps1"]
"masterlb" = "p-k8s-api.domain.tld:6443"
"vm_ip_master1" = "192.168.8.81"
"vm_ip_master2" = "192.168.8.82"
"vm_ip_master3" = "192.168.8.83"
"vm_ip_node1" = "192.168.8.84"
"vm_ip_node2" = "192.168.8.85"
"vm_ip_node3" = "192.168.8.86"
"vm_ip_node4" = "192.168.8.87"
"vm_ip_node5" = "192.168.8.88"
["kubernetes\03-traefik\network-policy.yaml"]
"vm_ip_master1" = "192.168.8.81"
"vm_ip_master2" = "192.168.8.82"
"vm_ip_master3" = "192.168.8.83"
"f5_ip1" = "192.168.8.150"
"f5_ip2" = "192.168.8.151"
"f5_ip3" = "192.168.8.152"
"f5_ip4" = "192.168.8.153"
"f5_ip5" = "192.168.8.154"
["kubernetes\05-dashboard\network-policy-template.yaml"]
"vm_ip_master1" = "192.168.8.81"
"vm_ip_master2" = "192.168.8.82"
"vm_ip_master3" = "192.168.8.83"
["kubernetes\05-dashboard\deploy.sh"]
"vm_name_master1" = "p-k8s-master-01"
"vm_name_master2" = "p-k8s-master-02"
"vm_name_master3" = "p-k8s-master-03"

```

In this example, `vm_ip_master1`, `vm_ip_master2` and `vm_ip_master3` are used in three different sections. To avoid repetition of values, you can move those variables out of a particular section to the top level to make them global. Global variables will apply to all templates. You also have an option of specifying those variables without values (use toml Boolean value `true`) in the sections corresponding to template that use the variable. This way you still need to update the value in a single place, but you also keep visibility of what's used where. You will get a warning if a global reference is missing or if it's present, but template does not use it. The example above, when modified, will look like this:

```toml
"vm_ip_master1" = "192.168.8.81"
"vm_ip_master2" = "192.168.8.82"
"vm_ip_master3" = "192.168.8.83"
["kubernetes\05-dashboard\ingress.yaml"]
"host" = "k8s-dashboard2.domain.tld"
["powershell\provision.ps1"]
"masterlb" = "p-k8s-api.domain.tld:6443"
"vm_ip_node1" = "192.168.8.84"
"vm_ip_node2" = "192.168.8.85"
"vm_ip_node3" = "192.168.8.86"
"vm_ip_node4" = "192.168.8.87"
"vm_ip_node5" = "192.168.8.88"
"vm_ip_master1" = true
"vm_ip_master2" = true
"vm_ip_master3" = true
["kubernetes\03-traefik\network-policy.yaml"]
"f5_ip1" = "192.168.8.150"
"f5_ip2" = "192.168.8.151"
"f5_ip3" = "192.168.8.152"
"f5_ip4" = "192.168.8.153"
"f5_ip5" = "192.168.8.154"
"vm_ip_master1" = true
"vm_ip_master2" = true
"vm_ip_master3" = true
["kubernetes\05-dashboard\network-policy-template.yaml"]
"vm_ip_master1" = true
"vm_ip_master2" = true
"vm_ip_master3" = true
["kubernetes\05-dashboard\deploy.sh"]
"vm_name_master1" = "p-k8s-master-01"
"vm_name_master2" = "p-k8s-master-02"
"vm_name_master3" = "p-k8s-master-03"
```

Note, that you do not _have_ to specify  `"vm_ip_master1" = true` and the likes in every section where it's used. It is useful to specify those, because it gives a better visibility what variables each template uses, and allows `octomerge` to produce meaningful warnings if discrepancies between the set of variables used in the toml file and the set of variables used in a template are detected. If you do not specify those lines, the `network-policy-template.yaml` section will look empty. This happens when all the variables a particular template requires are global. You will still need to specify a empty section like this, otherwise the corresponding template will not be processed. 

A local variable overrides the global one with the same name if both are specified.

There is no global variables in the single file mode.

Global variable are not examined during the unused variable detection in case when a variable is in the toml file but not in the template. Use `-g` to override that. This will result in warnings for every template that does not use any of the global variables, and in most cases is not what you want. `-s` is affected by warnings `-g` produces.

### Flags

- `-x` - Run in multifile mode. See above for details.
- `-q` - Quiet mode. Disables most warnings. If -p is also specified, does not also warn when there is a template variable that was left unsubstituted because there was no value for it in the toml file.
- `-g` - In multi-mode, for each template, warns about globals that are not used in the template. See above for details.
- `-p` - Do not terminate with a error if there are some variables in a template that do not have corresponding variables in the variables toml file. If this happens the result file (or files) will still have unsubstituted variables in it.
- `-s` - Treat warning described in the `-q` and -g switches as error and terminate if any of them happens. Note, that this strict mode is intended for "production" use, where the assumption is that should be no unfixed warnings. The program, therefore will terminate immediately when first such warning is encountered, it will not display possible subsequent warnings.
- `-v` - Verbose mode. Print out all variable names found in the variables toml file and in the template(s)
- `-d` - Dumps http response from Vault in case when a value cannot be read from Vault. Use with care, as the response can contain secrets in plain text.

## Running

Just run from the command line.


While not recommended, because of potential volume mapping problems, can be run from a docker image, e.g. without local binaries. On Windows:

```powershell
./octomerge.ps1 variables.toml file.txt.template result.txt
```

On Linux:

```bash
./octomerge.sh variables.toml file.txt.template result.txt
```

With docker please do not use absolute path or paths outside the current folder as they are not mapped to the docker container
by the script. 

## Vault integration

`octomerge` can pull variable values from Vault. To use this use the following format for variable value: `vault:secret_path:secret_key`, where `vault` is literal, `secret_path` is the path to your secret in vault, and `secret_key` is the key name under that path. For example a fragment of your `vars.toml` file can look like this:

```toml
["conf-k8s\secrets\grafana\smtp-password"]
"smtpPassword" = "vault:secret/operational/mail:smtpPassword"
```

If you are using Key-Value engine version 2, you have to indicate it by separating the engine mount point from the rest of the path by the "|" sign:

```toml
["conf-k8s\secrets\grafana\smtp-password"]
"smtpPassword" = "vault:Production|operational/mail:smtpPassword"
```

In this example "Production" is the mount point of the v2 KV secret engine you would like to use.

If `*` is used as the `secret_key` the entire secret in json format will be substituted.

Note, that `octomerge` utility relies on you being logged into vault when it's performing the substitution. Run `vault login ` to login, or supply the token in the `VAULT_TOKEN` environment variable. It also relies on the value of `VAULT_ADDR` environment variable to point to your.

## Building

### Local build

Can be build from Visual Studio by opening project or solution file, or from command line with `dotnet build`

### Docker build

Run `./build-docker.ps1` from PowerShell command prompt from the root folder of the app.  You need docker installed for this. You need to be logged to docker hub in order to push. You need to have write access to the repository you are pushing to (as specified in `build-docker.ps1`).

### GitHub Actions

In order to trigger a release via GitHub actions follow these steps:

* Check version in OctoMerge.csproj, make sure it's updated
* Run `git tag version` where `version` is something like `1.0.0`, should match to the first three components of the version from the previous step
* Run `git push --tags` This will push the tag to GitHub and will trigger a new build and release.

## Possible Improvements

- Allow vault values to be included as a part of a longer value (connection string)

  - What this means, is allowing to reference variable on the right side of equal sign in the toml file, which, in turn, means parsing them as octostache templates themselves.

- Allow to pass variable values from command line. One usage scenario is merging namespace name passed from `bt-deploy` (which is passed to `bt-deploy` as command line switch) for a ClusterRoleBinding object. Some ideas are here: <https://helm.sh/docs/helm/helm_install/>

- Ability to override values similar to (https://helm.sh/docs/helm/helm_install/)

- Extract variable by name from toml for use in scripts

- Ability to "tab" retrieved secrets for multiline `yaml` insertion (think TLS certificates)

- Better output: consider using stderr, consider using colors, review spurious new-line(s) output

- Merge with OctoVars

- OctoVars mode to generate "local globals" for all globals that are used in a template but not marked as such in toml

- Allow specifying different start folder for multifile mode

- Consider improving hardcoded `.template` suffix

- Display vault variable values when both `-v` and `-d` specified

  


## Changelog

- 1.0.12 - Added support for Vault version 2 kv engine, updated dependencies
- 1.0.11 - Updated to .net 5 
- 1.0.10 - Open-sourced, create GitHub actions build. Updated build scripts. Updated readme.
- 1.0.9
  - Added additional toml file validation. Now `octomerge` will warn you if there are elements or types it does not understand. They used to be ignored before
  - Added ability to specify "global reference". In a multi-file mode, if a particular template uses a global you can specify it without value (use `true` as a value) in that template section. This way you still have to change global in a single place if you need too, but you also get warnings in cases if a global is expected to be used in a template but is not used. Similarly a global reference without global and usage of a global without global reference also produce warnings.
- 1.0.8
  - Upgrade .net core from 2.2 => 3.1
  - Updated all dependencies to latest
  - Expanded readme with Globals and Unused variables detection
  - Do not warn by default if a global is not used in a template
  - Added `-g` flag to display a warning if a global is not used in a template
  - Reworked warnings and verbose to include `(global)` marker if a variable is global
  - Major refactoring to break down to smaller components
- 1.0.7 - Do not dump secrets in case of error if `-d` is not specified. Display program version on usage.
- 1.0.6 - Fixed exit code in case of errors
- 1.0.5 - Fixed typos in error message
- 1.0.4 - Display path that failed when vault query fails
- 1.0.3 - Allow to use VAULT_TOKEN environment variable for token value. Only if it's not found the token file will be queried
- 1.0.2 - disabled "/x" style of switches in favor of "-x" style, to keep the former interfering with Linux paths
- 1.0.1 - added ability to retrieve the entire Vault secret if `*` is specified as the key
