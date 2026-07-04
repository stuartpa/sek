This is a SIMPLIFIED version of model and adapter for SMB2 which 
captures some aspects of the protocol. This model is not intended to 
solve the SMB2 test suite problem, but only for demonstrating certain 
concepts.

In order to prepare for running this sample, you must:

- Ensure you have a folder c:\Temp
- Ensure you have a shared folder on your local machine (Vista or later) named "smb2test".
  Alternatively, you may also edit the Smb2Adapter.cs to use a different share
  on a different machine.
   

Once you have done so, you can do the following with this sample:

Regarding modeling:

- Explore machine "AllSync" in Config.cord.
- Study the content of Config.cord for more exploration goals and test generation.
- Study the file Model.cs. Observe how relative message ids are
  used to avoid state explosion present with absolute message ids,
  and how similar techniques are used to keep the number of file ids
  and tree ids finite.
