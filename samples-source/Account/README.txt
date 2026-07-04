This sample demonstrates how to use reference types as parameters of static actions. The TypeBindingAttribute is used to establish the mapping between model types and implementation/adapter types.

In this sample, a modeling type AccountDefinition is bound to an implementation type Account. In generated test code, all instances created by model will be mapped to implementation instances automatically. For example, the return value of SearchAccounts is a Set, where Spec Explorer can make the judgment if the implementation returning Set<Account> is the expected model returning Set<AccountDefinition>.

Note that when handling instances, the default domain is all instances created so far, domain generator cannot be applied to instances, that is why we do not need to assign domain for acount in SetBalance(AccountDefination account, float balance).

For an example of how to declare instance-based method as actions, select the corresponding option in the Spec Explorer model project wizard (visual Studio File menu -> New -> Project -> Visual C# -> Test -> Spec Explorer Model).
