This is a sample representing a "fake" protocol, called
MS-CHAT. It demonstrates some modeling patterns and the
usage of PTF messages framework to automatically 
marshal and unmarshal bits to the wire.

The protocol is a simple chat server which allows user
to logon, logoff, get a list of logged-on users, and
finally to publish a message which will be broadcasted
to all logged-on users. The protocol is block-oriented
and runs over TCP. A fake server is provided as well.

This sample also shows how to use Requirement Coverage 
Machine with combination of switches, RequirementsToCover
(set of requirements) and MinimumRequirementsCount
(minimun requirements covered in the generated graph).


You can run the tests of this project as follows:

1.  The project GeneratedTest contains test cases generated from a model. 
    To re-generate the test suite from the model, open Exploration Manager, 
    select machine TestSuite, and click "Generate Test Code". 
    You can also run this test project in Test Manager.
2.  All the test result can be observed in Test Results window.

