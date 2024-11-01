namespace System.Collections.ObjectModel;

public static class ObservableCollectionExtensions
{

	public static void AddRange<T>(this ObservableCollection<T> obj, IEnumerable<T> collection) => collection.ToList().ForEach(obj.Add);

}