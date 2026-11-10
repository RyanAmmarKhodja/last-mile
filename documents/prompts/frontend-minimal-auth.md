# Frontend service:
## Context:
the definitive goal is to implement a map to track delivery vehicles. we are not there yet for now we only need a minimal MVP to test authentication end-to-end.
what you are gonna do is build an interface : a login screen that takes us to /home that shows the details of the logged-in profile, such as username and role. ill need a /register endpoint too to register a few accounts before logging in. ill need an account settings page so that i can alter the account details and test. ill need to see the user roles in action. you can fill in the incoherences or missing features to accomplish this test.

## Infrastructure:
React project in /mile-portal
typescript


## Folder structure:
react stuff happens in /src
and every feature has it own folder, today's feature is authentication.
src/features/auth/...

### shared 
global reusable react functions, components, layouts are in /src/shared
it will include:
/api , which is the axios 
/components , reusable react components like buttons
/context , reusable context, notably the AuthContext
/layout , sidebar, navbar ..etc
/router the react router
/styles notably globals.css , site wide styles

### features
src/features/ contains the individual features or modules of the react project in today's session we will do the auth
a typical react feature folder struct is :
/pages the react pages go here
/components the individual components of the pages go here
/styles the styles of the components + pages go here

you may however create new folders if you judge it cleaner like /hooks or /utils.

## design style:
.NET 10 purple/blue , minimal, design is not priority


## frontend architecture: 
this should at the end be a container that interacts with our nginx backend gateway , i will need you at the end to connect this entire frontend to our microservice backend we just built earlier so that we may test authentication end-to-end

the end-to-end tests will be accomplished manually by me.

it is helpful to write a project-architecture-071126.md at the end which is a high level description of our project AS of TODAY. what we did so far. thank you in advance
