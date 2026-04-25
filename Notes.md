Preview in inifile generator not currently working enough
page inifilegenerator.razor

separate in two different component. make a gameinisection and a gameusersettingsinisection component please each one will have their things in it.

We need all that is a check marked i did some research for you heres the table name : 
wikitable config-table
The second one is the one for the gameusersettings. basicaly all that in there that has this : 
<img alt="Check mark.svg" src="/images/Check_mark.svg?3136d5" decoding="async" loading="lazy" width="20" height="20" data-file-width="600" data-file-height="600"> in its first column is needed
We need all variables and descriptions that first column has the image i posted in first column
Then check the value type and if its string or list then its a string input like for the active mods even if we manage active mods in settings
we are missing a lot. if you want for descriptions you can create a json file called en.json and do a key value pair under like gameusersettings.fieldname.description and gameusersettings.fieldname.title so that we have each ready to go and if we need traduction later we could reuse that in fr.
Do that please to start dont forget any fields that has the checkmark image and you can include also those that have this : 
<img alt="Missing.png" src="/images/thumb/Missing.png/20px-Missing.png?25f246" decoding="async" loading="lazy" width="20" height="20" data-file-width="256" data-file-height="256">
But as i said those are the question mark ones so the need to be validated if really usable or not.
